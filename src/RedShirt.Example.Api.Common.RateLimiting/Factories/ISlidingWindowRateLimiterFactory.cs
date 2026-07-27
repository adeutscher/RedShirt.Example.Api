using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.Factories;

internal interface ISlidingWindowRateLimiterFactory
{
    RateLimitPartition<string> GetRateLimiter(string partitionKey, RateLimitingPolicyOptions policyOptions);
}