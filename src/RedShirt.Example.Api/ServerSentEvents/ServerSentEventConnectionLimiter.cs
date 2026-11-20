using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace RedShirt.Example.Api.ServerSentEvents;

/// <summary>
///     Counts open event streams per user on this API instance.
/// </summary>
public interface IServerSentEventConnectionLimiter
{
    /// <summary>
    ///     Reserves a stream slot for <paramref name="userId" />. Returns <see langword="false" /> when the user is
    ///     already at <see cref="ServerSentEventsOptions.MaxConnectionsPerUser" />. Dispose the lease when the stream
    ///     ends to free the slot. Disposing it more than once is safe.
    /// </summary>
    bool TryAcquire(string userId, [NotNullWhen(true)] out IDisposable? lease);
}

internal sealed class ServerSentEventConnectionLimiter(IOptions<ServerSentEventsOptions> options)
    : IServerSentEventConnectionLimiter
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly int _limit = options.Value.MaxConnectionsPerUser;
    private readonly Lock _lock = new();

    public bool TryAcquire(string userId, [NotNullWhen(true)] out IDisposable? lease)
    {
        if (_limit <= 0)
        {
            lease = UnlimitedLease.Instance;
            return true;
        }

        // Streams open and close far less often than requests, so a single lock is cheap enough here.
        lock (_lock)
        {
            var count = _counts.GetValueOrDefault(userId);
            if (count >= _limit)
            {
                lease = null;
                return false;
            }

            _counts[userId] = count + 1;
        }

        lease = new Lease(this, userId);
        return true;
    }

    /// <summary>
    ///     Number of streams currently counted for <paramref name="userId" />. For tests and diagnostics.
    /// </summary>
    internal int GetOpenCount(string userId)
    {
        lock (_lock)
        {
            return _counts.GetValueOrDefault(userId);
        }
    }

    private void Release(string userId)
    {
        lock (_lock)
        {
            if (!_counts.TryGetValue(userId, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                // Drop the entry so users who have disconnected do not keep memory.
                _counts.Remove(userId);
            }
            else
            {
                _counts[userId] = count - 1;
            }
        }
    }

    private sealed class Lease(ServerSentEventConnectionLimiter limiter, string userId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                limiter.Release(userId);
            }
        }
    }

    private sealed class UnlimitedLease : IDisposable
    {
        public static readonly UnlimitedLease Instance = new();

        public void Dispose()
        {
        }
    }
}
