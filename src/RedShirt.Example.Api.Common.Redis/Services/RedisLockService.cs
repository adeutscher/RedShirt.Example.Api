using Medallion.Threading.Redis;
using RedShirt.Example.Api.Common.Abstractions;
using RedShirt.Example.Api.Common.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.Services;

internal class RedisLockService(IRedisSharedConnectionService redisConnectionService) : IAbstractedLockService
{
    private static readonly TimeSpan LockAttemptThreshold = TimeSpan.FromSeconds(5);

    private IConnectionMultiplexer? _redisConnection;

    public async Task<IAbstractedLock> GetLockAsync(string key, CancellationToken cancellationToken = default)
    {
        _redisConnection ??= await redisConnectionService.GetConnectionAsync(cancellationToken);
        var redisLock = new RedisDistributedLock(key, _redisConnection.GetDatabase());
        var lockHandle = await redisLock.TryAcquireAsync(LockAttemptThreshold, cancellationToken);
        return new DistributedLock(lockHandle);
    }

    private sealed class DistributedLock(RedisDistributedLockHandle? lockHandle) : IAbstractedLock
    {
        public bool IsAcquired => lockHandle is not null;

        public void Unlock()
        {
            lockHandle?.Dispose();
        }
    }
}