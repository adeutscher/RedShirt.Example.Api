using RedShirt.Example.Api.Common.RateLimiting.Constants;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.RateLimiters;

internal sealed class SlidingWindowRateLimitLease(
    bool isAcquired,
    long? remaining = null,
    long? limit = null,
    TimeSpan? retryAfter = null,
    Action? onDispose = null)
    : RateLimitLease
{
    private int _disposed;

    public override bool IsAcquired { get; } = isAcquired;

    public override IEnumerable<string> MetadataNames
    {
        get
        {
            if (limit is not null)
            {
                yield return RateLimitMetadata.PermitLimit.Name;
            }

            if (remaining is not null)
            {
                yield return RateLimitMetadata.RemainingPermits.Name;
            }

            if (retryAfter is not null)
            {
                yield return MetadataName.RetryAfter.Name;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            onDispose?.Invoke();
        }
    }

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (limit is not null
            && string.Equals(metadataName, RateLimitMetadata.PermitLimit.Name, StringComparison.Ordinal))
        {
            metadata = limit.Value;
            return true;
        }

        if (remaining is not null
            && string.Equals(metadataName, RateLimitMetadata.RemainingPermits.Name, StringComparison.Ordinal))
        {
            metadata = remaining.Value;
            return true;
        }

        if (retryAfter is not null
            && string.Equals(metadataName, MetadataName.RetryAfter.Name, StringComparison.Ordinal))
        {
            metadata = retryAfter.Value;
            return true;
        }

        metadata = null;
        return false;
    }
}