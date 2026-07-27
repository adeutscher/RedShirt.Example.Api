using RedShirt.Example.Api.Common.Redis.Factories;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.Services;

public interface IRedisSharedConnectionService
{
    Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default);
}

internal class RedisSharedConnectionService(IRedisConnectionFactory redisConnectionFactory)
    : IRedisSharedConnectionService
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private IConnectionMultiplexer? _connection;

    public async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            _connection ??= await redisConnectionFactory.GetConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}