using MySql.Data.MySqlClient;
using RedShirt.Example.Api.Common.Database.DapperMySql.Models;
using RedShirt.Example.Api.Common.Database.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using System.Net.Sockets;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Services.Resilience;

/// <summary>
///     Classifies MySQL / Dapper exceptions for retry decisions.
/// </summary>
internal interface IMySqlExceptionArbiterService
{
    MySqlExceptionArbiterReport GetReport(Exception exception);
}

/// <summary>
///     MySQL-oriented exception arbiter modelled after the Azure / Redis exception arbiters:
///     known infrastructure failures may be transient; caller cancel and bad arguments are not.
///     <see cref="MySqlException.IsTransient" /> is honoured when true; known MySQL error numbers are also classified
///     because Connector/NET often leaves <see cref="MySqlException.IsTransient" /> false for retryable conditions.
/// </summary>
internal sealed class MySqlExceptionArbiterService : IMySqlExceptionArbiterService
{
    /// <summary>
    ///     MySQL error numbers that typically indicate brief connectivity, concurrency, or server-capacity issues.
    /// </summary>
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        1040, // Too many connections
        1053, // Server shutdown in progress
        1184, // Aborted connection
        1205, // Lock wait timeout exceeded
        1213, // Deadlock found when trying to get lock
        1614, // Transaction branch was rolled back (XA)
        1927, // Connection was killed
        2002, // Can't connect to local MySQL server
        2003, // Can't connect to MySQL server
        2006, // MySQL server has gone away
        2013 // Lost connection to MySQL server during query
    ];

    /// <summary>
    ///     Auth / privilege failures that ops can fix externally (credentials, grants) without retrying locally.
    /// </summary>
    private static readonly HashSet<int> ExternallySolvableAuthErrorNumbers =
    [
        1044, // Access denied for user to database
        1045, // Access denied for user
        1142, // command denied to user
        1227 // Access denied; you need privilege
    ];

    private static MySqlExceptionArbiterReport Fresh(bool isExpected, bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new MySqlExceptionArbiterReport
        {
            AlreadyHandled = false,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static MySqlExceptionArbiterReport Handled(
        bool isExpected,
        bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new MySqlExceptionArbiterReport
        {
            AlreadyHandled = true,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static MySqlExceptionArbiterReport ClassifyMySqlException(MySqlException exception)
    {
        if (exception.IsTransient || TransientErrorNumbers.Contains(exception.Number))
        {
            return Fresh(true, true, true);
        }

        if (ExternallySolvableAuthErrorNumbers.Contains(exception.Number))
        {
            return Fresh(true, false, true);
        }

        // Other MySQL errors (syntax, constraint, missing table, etc.) are expected but not retryable.
        return Fresh(true, false, false);
    }

    public MySqlExceptionArbiterReport GetReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        while (exception is AggregateException {InnerExceptions.Count: 1, InnerException: not null} aggregate)
        {
            exception = aggregate.InnerException!;
        }

        return exception switch
        {
            // Already classified/wrapped by an earlier Database layer — do not wrap again.
            ApiDatabaseException w =>
                Handled(true, w is {IsHandled: false, CouldBeTransient: true}, w.CouldBeExternallySolvable),
            // Connection strings often come from a secret manager; honour prior classification.
            ApiSecretManagerException w =>
                Handled(true, w is {IsHandled: false, CouldBeTransient: true}, w.CouldBeExternallySolvable),
            MySqlException mySql => ClassifyMySqlException(mySql),
            TimeoutException
                or SocketException => Fresh(true, true, true),
            // HttpClient-style / connector timeouts sometimes surface as TaskCanceledException.
            // Must be matched before OperationCanceledException (TCE derives from OCE).
            TaskCanceledException => Fresh(true, true, true),
            // Explicit CancellationToken cancellation from the caller — do not retry.
            OperationCanceledException => Fresh(true, false, false),
            // Client-side argument validation — bad local configuration/arguments, not retryable.
            ArgumentException => Fresh(true, false, false),
            // Unrecognized exception type — treat as unexpected so callers surface the raw failure.
            _ => Fresh(false, false, false)
        };
    }
}
