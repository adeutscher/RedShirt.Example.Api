namespace RedShirt.Example.Api.Connectors.Common.Http.Extensions;

public static class HttpResponseMessageExtensions
{
    /// <summary>
    ///     Reads the <c>Retry-After</c> header as a delay, or null when the response had no usable value.
    ///     A date already past <paramref name="utcNow" /> means the dependency is ready now.
    /// </summary>
    public static TimeSpan? GetRetryAfter(this HttpResponseMessage response, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            return date > utcNow ? date - utcNow : TimeSpan.Zero;
        }

        return null;
    }
}