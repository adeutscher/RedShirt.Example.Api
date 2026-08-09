using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using System.Text.Json;

namespace RedShirt.Example.Api.Common.Distributed.Extensions;

public static class RemoteCacheExtensions
{
    public static async Task<T?> GetObjectAsync<T>(this IRemoteCacheService remoteCacheService, string key,
        CancellationToken cancellationToken = default) where T : class
    {
        var value = await remoteCacheService.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            // pass
            return null;
        }
    }

    public static Task SetObjectAsync<T>(this IRemoteCacheService remoteCacheService, string key, T value,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) where T : class
    {
        return remoteCacheService.SetStringAsync(key, JsonSerializer.Serialize(value), expiry, cancellationToken);
    }
}