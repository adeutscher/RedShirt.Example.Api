using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.RateLimiters;

/// <summary>
///     Distributed sliding-window limiter backed by a Redis sorted set (request timestamps as scores).
/// </summary>
internal sealed class RedisSlidingWindowRateLimiter(
    IDatabase redisDatabase,
    string redisKey,
    int permitLimit,
    TimeSpan window,
    bool failClosed,
    IHttpContextAccessor httpContextAccessor,
    ILogger logger)
    : RateLimiter
{
    /// <summary>
    ///     This Lua script allows for sliding-window operations to be atomic.
    /// </summary>
    private static readonly LuaScript Script = LuaScript.Prepare("""
                                                                 local key = @key
                                                                 local now = tonumber(@now)
                                                                 local window = tonumber(@window)
                                                                 local limit = tonumber(@limit)
                                                                 local member = @member

                                                                 redis.call('ZREMRANGEBYSCORE', key, 0, now - window)
                                                                 local count = redis.call('ZCARD', key)

                                                                 if count < limit then
                                                                   redis.call('ZADD', key, now, member)
                                                                   redis.call('PEXPIRE', key, window)
                                                                   return {1, limit - count - 1, 0}
                                                                 end

                                                                 local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                                                                 local retryAfter = window
                                                                 if oldest[2] ~= nil then
                                                                   retryAfter = math.max(0, window - (now - tonumber(oldest[2])))
                                                                 end
                                                                 return {0, 0, retryAfter}
                                                                 """);

    private readonly long _windowMilliseconds = (long) window.TotalMilliseconds;
    private int _activeRequests;

    private SlidingWindowRateLimitLease FailOpenOrClosed(string reason)
    {
        if (failClosed)
        {
            return new SlidingWindowRateLimitLease(false, 0, permitLimit, TimeSpan.FromSeconds(1));
        }

        logger.LogWarning("Rate limiter failing open: {Reason}", reason);
        return new SlidingWindowRateLimitLease(true);
    }

    public override TimeSpan? IdleDuration => null;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);

        if (permitCount > 1)
        {
            // This limiter issues one permit per request (matches transfer endpoint usage).
            return new SlidingWindowRateLimitLease(false, 0, permitLimit, TimeSpan.Zero);
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var member = $"{now}-{Guid.NewGuid():N}";

            var result = (RedisResult[]?) await Script.EvaluateAsync(
                redisDatabase,
                new
                {
                    key = (RedisKey) redisKey,
                    now,
                    window = _windowMilliseconds,
                    limit = permitLimit,
                    member
                });

            if (result is null || result.Length < 3)
            {
                return FailOpenOrClosed("Unexpected Redis script response.");
            }

            var remaining = (long) result[1];
            var allowed = (int) result[0] == 1;
            if (allowed)
            {
                RateLimitHeaderWriter.ConsiderWritingHeaders(httpContextAccessor, permitLimit, remaining);
                Interlocked.Increment(ref _activeRequests);
                return new SlidingWindowRateLimitLease(true, remaining, permitLimit,
                    onDispose: () => Interlocked.Decrement(ref _activeRequests));
            }

            var retryAfterMs = (long) result[2];
            return new SlidingWindowRateLimitLease(false, 0, permitLimit,
                TimeSpan.FromMilliseconds(retryAfterMs));
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis sliding-window rate limiter failed for key {RedisKey}", redisKey);
            return FailOpenOrClosed(ex.Message);
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        return AcquireAsyncCore(permitCount, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return default;
    }

    public override RateLimiterStatistics GetStatistics()
    {
        return new RateLimiterStatistics
        {
            CurrentAvailablePermits = Math.Max(0, permitLimit - _activeRequests),
            CurrentQueuedCount = 0,
            TotalSuccessfulLeases = 0,
            TotalFailedLeases = 0
        };
    }
}