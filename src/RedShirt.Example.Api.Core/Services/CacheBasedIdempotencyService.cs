using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Distributed.Models;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using System.Text.Json;

namespace RedShirt.Example.Api.Core.Services;

/// <summary>
///     Perform idempotent operation. Repeated attempts will be served out of a cache.
/// </summary>
public interface ICacheBasedIdempotencyService
{
    Task<IAbstractedLock> GetLockAsync(IdempotencyScope scope, CancellationToken cancellationToken = default);
    Task<T?> GetRecordAsync<T>(IdempotencyScope scope, CancellationToken cancellationToken = default);
    Task SetRecordAsync<T>(IdempotencyScope scope, T value, CancellationToken cancellationToken = default);
}

public class CacheBasedIdempotencyService(
    IRemoteCacheService dataCacheService,
    IAbstractedLockService lockService,
    IOptions<CacheBasedIdempotencyService.ConfigurationModel> config) : ICacheBasedIdempotencyService
{
    internal const string LockKeyPrefix = "idempotent-concurrency:";
    internal const string RecordKeyPrefix = "idempotent-record:";

    public Task<IAbstractedLock> GetLockAsync(IdempotencyScope scope, CancellationToken cancellationToken = default)
    {
        return lockService.GetLockAsync(LockKeyPrefix + scope.ToCacheKeyFragment(), cancellationToken);
    }

    public async Task<T?> GetRecordAsync<T>(IdempotencyScope scope, CancellationToken cancellationToken = default)
    {
        if (await dataCacheService.GetStringAsync(RecordKeyPrefix + scope.ToCacheKeyFragment(), cancellationToken)
            is not { } dataString)
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

    public Task SetRecordAsync<T>(IdempotencyScope scope, T value, CancellationToken cancellationToken = default)
    {
        return dataCacheService.SetStringAsync(RecordKeyPrefix + scope.ToCacheKeyFragment(),
            JsonSerializer.Serialize(value),
            TimeSpan.FromMinutes(config.Value.EffectiveIdempotencyTimeMinutes), cancellationToken);
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