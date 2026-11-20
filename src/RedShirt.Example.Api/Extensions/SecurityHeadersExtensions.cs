using Microsoft.Net.Http.Headers;

namespace RedShirt.Example.Api.Extensions;

internal static class SecurityHeadersExtensions
{
    /// <summary>
    ///     Responses are JSON (or an event stream), never a page, so nothing may be loaded or framed from them.
    /// </summary>
    internal const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>
    ///     Swagger UI (Development only) runs inline scripts and styles, so it keeps the other headers but not the API
    ///     content security policy.
    /// </summary>
    internal static readonly PathString SwaggerPathPrefix = new("/swagger");

    private static void ApplySecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.XFrameOptions] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        if (!context.Request.Path.StartsWithSegments(SwaggerPathPrefix))
        {
            headers[HeaderNames.ContentSecurityPolicy] = ApiContentSecurityPolicy;
        }

        if (!headers.ContainsKey(HeaderNames.CacheControl))
        {
            headers[HeaderNames.CacheControl] = "no-store";
        }
    }

    /// <summary>
    ///     Adds defensive response headers to every response.
    ///     <para>
    ///         The headers are written when the response starts, so responses produced after the exception handler or
    ///         status code pages middleware have cleared the response still get them. Register this first in the
    ///         pipeline. <c>Cache-Control: no-store</c> is only added when nothing else set a caching policy: API
    ///         responses can carry customer and order data that shared caches must not keep.
    ///     </para>
    /// </summary>
    internal static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                ApplySecurityHeaders(context);
                return Task.CompletedTask;
            });
            return next(context);
        });
        return app;
    }
}