namespace RedShirt.Example.Api.Extensions;

public static class HttpRequestExceptions
{
    public static Uri GetPublicBaseUri(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Port = request.Host.Port ?? -1,
            Path = request.PathBase.HasValue ? request.PathBase.Value! : "/"
        };
        if (builder.Port is 80 or 443)
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }
}