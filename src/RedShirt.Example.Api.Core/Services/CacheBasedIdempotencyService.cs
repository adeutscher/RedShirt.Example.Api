using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Abstractions;
using RedShirt.Example.Api.Common.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.Core.Services;

/// <summary>
///     Perform idempotent operation. Repeated attempts will be served out of a cache.
/// </summary>
public interface ICacheBasedIdempotencyService
{
    Task<IAbstractedLock> GetLockAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetRecordAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetRecordAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

public class CacheBasedIdempotencyService(
    IDataCacheService dataCacheService,
    IAbstractedLockService lockService,
    IOptions<CacheBasedIdempotencyService.ConfigurationModel> config) : ICacheBasedIdempotencyService
{
    public Task<IAbstractedLock> GetLockAsync(string key, CancellationToken cancellationToken = default)
    {
        return lockService.GetLockAsync($"idempotent-concurrency:{key}");
    }

    public async Task<T?> GetRecordAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (await dataCacheService.GetStringAsync(key) is not { } dataString)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(dataString);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public Task SetRecordAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        return dataCacheService.SetStringAsync(key, JsonSerializer.Serialize(value),
            TimeSpan.FromMinutes(config.Value.EffectiveIdempotencyTimeMinutes));
    }

    public sealed class ConfigurationModel
    {
        /// <summary>
        ///     Amount of time that idempotent operations should be tracked for.
        /// </summary>
        public required int IdempotencyTrackingTimeMinutes { get; init; }

        public int EffectiveIdempotencyTimeMinutes => Math.Max(1, IdempotencyTrackingTimeMinutes);
    }
}