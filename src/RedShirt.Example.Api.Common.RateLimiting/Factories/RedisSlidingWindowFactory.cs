using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.RateLimiters;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.Factories;

internal interface IRedisSlidingWindowRateLimiterFactory : ISlidingWindowRateLimiterFactory
{
    void Initialize(string? policyName, IDatabase? redisDatabase);
}

internal class RedisSlidingWindowFactory(IServiceProvider serviceProvider) : IRedisSlidingWindowRateLimiterFactory
{
    private string? _policyName;
    private IDatabase? _redisDatabase;

    public RateLimitPartition<string> GetRateLimiter(string partitionKey, RateLimitingPolicyOptions policyOptions)
    {
        if (_redisDatabase is null)
        {
            throw new InvalidOperationException(
                "Attempting to produce rate limiter without initialization. Developer error.");
        }

        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<RedisSlidingWindowRateLimiter>();
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

        var prefix = string.IsNullOrWhiteSpace(policyOptions.RedisKeyPrefix) ? "redis" : policyOptions.RedisKeyPrefix;

        return RateLimitPartition.Get(
            partitionKey,
            key => new RedisSlidingWindowRateLimiter(
                _redisDatabase,
                $"{prefix}:{_policyName ?? prefix}:{key}",
                policyOptions.WindowPermitLimit,
                policyOptions.Window,
                policyOptions.FailClosed,
                httpContextAccessor,
                logger));
    }

    public void Initialize(string? policyName, IDatabase? redisDatabase)
    {
        _policyName = policyName;
        _redisDatabase = redisDatabase;
    }
}