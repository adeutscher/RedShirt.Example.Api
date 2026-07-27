using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.Constants;

internal static class RateLimitMetadata
{
    public static readonly MetadataName<long> PermitLimit = MetadataName.Create<long>("RATE_LIMIT_LIMIT");
    public static readonly MetadataName<long> RemainingPermits = MetadataName.Create<long>("RATE_LIMIT_REMAINING");
}