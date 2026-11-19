using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.Connectors.Bar.Core.Exceptions;
using RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;

namespace RedShirt.Example.Api.ExceptionHandlers;

/// <summary>
///     Maps known domain exceptions to ProblemDetails HTTP responses.
///     <para>
///         Only exceptions written for clients (the <c>Common.Exceptions.Responses</c> types and the connectors'
///         record-not-found exceptions) put their message in the response. Upstream failures get a fixed detail: their
///         messages can carry internal host names, ports, or upstream response text, so they are logged server-side
///         instead. For local debugging, <see cref="ExceptionHandlingOptions.ExposeUpstreamExceptionMessages" />
///         puts them back in the response, in the Development environment only.
///     </para>
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IOptions<ExceptionHandlingOptions> options,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private readonly bool _exposeUpstreamExceptionMessages =
        options.Value.EffectiveExposeUpstreamExceptionMessages(environment);

    internal const string UpstreamFailureDetail =
        "A service this API depends on failed or is unavailable. Try again later.";

    private static bool TryMapException(Exception exception, out int statusCode, out string title,
        out bool isUpstreamFailure)
    {
        isUpstreamFailure = false;
        switch (exception)
        {
            case BadRequestException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                return true;
            case ResourceNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                return true;
            case ConflictException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                return true;
            case RequestTooLargeException:
                statusCode = StatusCodes.Status413PayloadTooLarge;
                title = "Payload Too Large";
                return true;
            case NoChangesToModifyException:
                statusCode = StatusCodes.Status304NotModified;
                title = "Not Modified";
                return true;
            case FooRecordNotFoundException:
            case BarRecordNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                return true;
            case FooConnectorException:
            case BarConnectorException:
            case ApiClientEventsException:
                statusCode = StatusCodes.Status502BadGateway;
                title = "Bad Gateway";
                isUpstreamFailure = true;
                return true;
            default:
                statusCode = 0;
                title = string.Empty;
                return false;
        }
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (!TryMapException(exception, out var statusCode, out var title, out var isUpstreamFailure))
        {
            return false;
        }

        if (isUpstreamFailure)
        {
            // The response hides the cause, so record it here. Handled exceptions are not logged by the exception
            // handler middleware.
            logger.LogError(exception, "Upstream failure while handling {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = isUpstreamFailure && !_exposeUpstreamExceptionMessages
                    ? UpstreamFailureDetail
                    : string.IsNullOrWhiteSpace(exception.Message)
                        ? null
                        : exception.Message
            }
        });
    }
}