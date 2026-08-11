using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Connectors.Bar.Core.Exceptions;
using RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;

namespace RedShirt.Example.Api.ExceptionHandlers;

/// <summary>
///     Maps known domain exceptions to ProblemDetails HTTP responses.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static bool TryMapException(Exception exception, out int statusCode, out string title)
    {
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
            case NoChangesToModifyException:
                statusCode = StatusCodes.Status304NotModified;
                title = "Not Modified";
                return true;
            case FooRecordNotFoundException:
            case BarRecordNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                return true;
            case FooUnauthorizedException:
            case FooConnectorException:
            case BarConnectorException:
                statusCode = StatusCodes.Status502BadGateway;
                title = "Bad Gateway";
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
        if (!TryMapException(exception, out var statusCode, out var title))
        {
            return false;
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
                Detail = string.IsNullOrWhiteSpace(exception.Message) ? null : exception.Message
            }
        });
    }
}