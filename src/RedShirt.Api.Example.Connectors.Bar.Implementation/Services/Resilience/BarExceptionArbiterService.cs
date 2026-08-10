using RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Models;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;

/// <summary>
///     Classifies Bar connector exceptions for retry decisions.
/// </summary>
internal interface IBarExceptionArbiterService
{
    BarExceptionArbiterReport GetReport(Exception exception);
}

/// <summary>
///     Bar-oriented exception arbiter modelled after the MySQL / Azure exception arbiters:
///     known infrastructure and retryable HTTP failures may be transient; caller cancel and bad arguments are not.
/// </summary>
internal sealed class BarExceptionArbiterService : IBarExceptionArbiterService
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

    private static BarExceptionArbiterReport Fresh(bool isExpected, bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new BarExceptionArbiterReport
        {
            AlreadyHandled = false,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static BarExceptionArbiterReport Handled(
        bool isExpected,
        bool couldBeTransient,
        bool couldBeExternallySolvable)
    {
        return new BarExceptionArbiterReport
        {
            AlreadyHandled = true,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };
    }

    private static BarExceptionArbiterReport ClassifyHttpRequestException(HttpRequestException exception)
    {
        // No HTTP response (DNS, connection refused, TLS errors, etc.) — treat as transient infra.
        if (exception.StatusCode is null)
        {
            return Fresh(true, true, true);
        }

        var status = (int) exception.StatusCode.Value;
        if (TransientHttpStatuses.Contains(status))
        {
            return Fresh(true, true, true);
        }

        // Auth / not-found style failures can be fixed externally; do not retry locally.
        if (exception.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound)
        {
            return Fresh(true, false, true);
        }

        // Other HTTP client errors (validation, conflict, etc.) are expected but not retryable.
        return Fresh(true, false, false);
    }

    public BarExceptionArbiterReport GetReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        while (exception is AggregateException {InnerExceptions.Count: 1, InnerException: not null} aggregate)
        {
            exception = aggregate.InnerException!;
        }

        return exception switch
        {
            BarRecordNotFoundException => Fresh(true, false, false),
            BarUnauthorizedException => Fresh(true, false, true),
            BarConnectorException w =>
                Handled(true, w is {IsHandled: false, CouldBeTransient: true}, w.CouldBeExternallySolvable),
            // API key comes from the secret manager; honour prior classification.
            ApiSecretManagerException w =>
                Handled(true, w is {IsHandled: false, CouldBeTransient: true}, w.CouldBeExternallySolvable),
            HttpRequestException http => ClassifyHttpRequestException(http),
            SocketException
                or TimeoutException => Fresh(true, true, true),
            // Malformed / unexpected payload — expected from a bad response, not retryable.
            JsonException => Fresh(true, false, false),
            // HttpClient timeouts commonly surface as TaskCanceledException.
            // Must be matched before OperationCanceledException (TCE derives from OCE).
            TaskCanceledException => Fresh(true, true, true),
            OperationCanceledException => Fresh(true, false, false),
            ArgumentException => Fresh(true, false, false),
            _ => Fresh(false, false, false)
        };
    }
}