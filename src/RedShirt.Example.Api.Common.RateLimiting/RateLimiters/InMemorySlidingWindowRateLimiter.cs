using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.RateLimiters;

/// <summary>
///     Process-local sliding-window limiter backed by request timestamps in memory.
/// </summary>
internal sealed class InMemorySlidingWindowRateLimiter(
    int permitLimit,
    TimeSpan window,
    IHttpContextAccessor httpContextAccessor)
    : RateLimiter
{
    private readonly Lock _gate = new();
    private readonly Queue<long> _timestamps = new();
    private readonly long _windowMilliseconds = (long) window.TotalMilliseconds;
    private int _activeRequests;

    public override TimeSpan? IdleDuration => null;

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        return new ValueTask<RateLimitLease>(AttemptAcquireCore(permitCount));
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);

        if (permitCount > 1)
        {
            return new SlidingWindowRateLimitLease(false, 0, permitLimit, TimeSpan.Zero);
        }

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (_timestamps.Count > 0 && _timestamps.Peek() <= now - _windowMilliseconds)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count < permitLimit)
            {
                _timestamps.Enqueue(now);
                var remaining = permitLimit - _timestamps.Count;
                RateLimitHeaderWriter.ConsiderWritingHeaders(httpContextAccessor, permitLimit, remaining);
                Interlocked.Increment(ref _activeRequests);
                return new SlidingWindowRateLimitLease(true, remaining, permitLimit,
                    onDispose: () => Interlocked.Decrement(ref _activeRequests));
            }

            var oldest = _timestamps.Peek();
            var retryAfterMs = Math.Max(0, _windowMilliseconds - (now - oldest));
            return new SlidingWindowRateLimitLease(false, 0, permitLimit,
                TimeSpan.FromMilliseconds(retryAfterMs));
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return default;
    }

    public override RateLimiterStatistics? GetStatistics()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (_timestamps.Count > 0 && _timestamps.Peek() <= now - _windowMilliseconds)
            {
                _timestamps.Dequeue();
            }

            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = Math.Max(0, permitLimit - _timestamps.Count),
                CurrentQueuedCount = 0,
                TotalSuccessfulLeases = 0,
                TotalFailedLeases = 0
            };
        }
    }
}