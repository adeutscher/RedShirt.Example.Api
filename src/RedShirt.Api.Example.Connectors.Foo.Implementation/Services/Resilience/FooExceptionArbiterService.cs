using RedShirt.Api.Example.Connectors.Foo.Implementation.Exceptions;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Models;
using System.Net.Sockets;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;

/// <summary>
///     Classifies Foo connector exceptions for retry decisions.
/// </summary>
internal interface IFooExceptionArbiterService
{
    FooExceptionArbiterReport GetReport(Exception exception);
}

/// <summary>
///     Foo-oriented exception arbiter modelled after the MySQL / Azure exception arbiters:
///     known infrastructure and retryable HTTP failures may be transient; caller cancel and bad arguments are not.
/// </summary>
internal sealed class FooExceptionArbiterService : IFooExceptionArbiterService
{
    private static readonly HashSet<int> TransientHttpStatuses =
    [
        408,
        429,
        500,
        502,
        503,
        504
    ];

    private static FooExceptionArbiterReport Fresh(bool isExpected, bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new FooExceptionArbiterReport
        {
            AlreadyHandled = false,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static FooExceptionArbiterReport Handled(
        bool isExpected,
        bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new FooExceptionArbiterReport
        {
            AlreadyHandled = true,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static FooExceptionArbiterReport ClassifyFooException(FooConnectorException exception)
    {
        // No HTTP response (DNS, connection refused, timeout wrapped earlier) — treat as transient infra.
        if (exception.StatusCode is null)
        {
            return Fresh(true, true, true);
        }

        var status = exception.StatusCode.Value;
        if (TransientHttpStatuses.Contains(status))
        {
            return Fresh(true, true, true);
        }

        // Auth / not-found style failures can be fixed externally; do not retry locally.
        if (status is 401 or 403 or 404)
        {
            return Fresh(true, false, true);
        }

        // Other HTTP client errors (validation, conflict, etc.) are expected but not retryable.
        return Fresh(true, false, false);
    }

    public FooExceptionArbiterReport GetReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        while (exception is AggregateException {InnerExceptions.Count: 1, InnerException: not null} aggregate)
        {
            exception = aggregate.InnerException!;
        }

        return exception switch
        {
            ApiFooConnectorException w =>
                Handled(true, w is {IsHandled: false, CouldBeTransient: true}, w.CouldBeExternallySolvable),
            FooConnectorException foo => ClassifyFooException(foo),
            HttpRequestException
                or SocketException
                or TimeoutException => Fresh(true, true, true),
            // HttpClient timeouts commonly surface as TaskCanceledException.
            // Must be matched before OperationCanceledException (TCE derives from OCE).
            TaskCanceledException => Fresh(true, true, true),
            OperationCanceledException => Fresh(true, false, false),
            ArgumentException => Fresh(true, false, false),
            _ => Fresh(false, false, false)
        };
    }
}
