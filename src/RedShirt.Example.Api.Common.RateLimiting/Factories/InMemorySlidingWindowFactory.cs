using Microsoft.AspNetCore.Http;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.RateLimiters;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.Factories;

internal interface IInMemorySlidingWindowFactory : ISlidingWindowRateLimiterFactory
{
    public void Initialize(string policyName);
}

internal class InMemorySlidingWindowFactory(IHttpContextAccessor httpContextAccessor) : IInMemorySlidingWindowFactory
{
    private string? _policyName;

    public RateLimitPartition<string> GetRateLimiter(string partitionKey, RateLimitingPolicyOptions policyOptions)
    {
        return RateLimitPartition.Get(
            $"{partitionKey}:{_policyName}",
            _ => new InMemorySlidingWindowRateLimiter(
                policyOptions.WindowPermitLimit,
                policyOptions.Window,
                httpContextAccessor));
    }

    public void Initialize(string policyName)
    {
        _policyName = policyName;
    }
}