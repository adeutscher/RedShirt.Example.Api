using Microsoft.AspNetCore.Http;
using RedShirt.Example.Api.Common.RateLimiting.Constants;

namespace RedShirt.Example.Api.Common.RateLimiting.RateLimiters;

internal static class RateLimitHeaderWriter
{
    public static void ConsiderWritingHeaders(
        IHttpContextAccessor httpContextAccessor,
        int permitLimit,
        long remaining)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[RateLimitHeaderNames.Limit] = permitLimit.ToString();
            httpContext.Response.Headers[RateLimitHeaderNames.Remaining] = remaining.ToString();
            return Task.CompletedTask;
        });
    }
}