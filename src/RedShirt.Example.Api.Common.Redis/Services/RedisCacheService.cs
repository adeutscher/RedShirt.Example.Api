using RedShirt.Example.Api.Common.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.Services;

internal class RedisCacheService(IRedisSharedConnectionService redisConnectionService) : IDataCacheService
{
    private IConnectionMultiplexer? _redisConnection;

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        _redisConnection ??= await redisConnectionService.GetConnectionAsync(cancellationToken);
        return await _redisConnection.GetDatabase().StringGetAsync(key);
    }

    public async Task SetStringAsync(string key, string value, TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _redisConnection ??= await redisConnectionService.GetConnectionAsync(cancellationToken);
        await _redisConnection.GetDatabase().StringSetAsync(key, value, expiration);
    }
}