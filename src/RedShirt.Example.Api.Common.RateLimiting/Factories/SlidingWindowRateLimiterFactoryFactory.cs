using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.Redis.Services;

namespace RedShirt.Example.Api.Common.RateLimiting.Factories;

internal interface ISlidingWindowRateLimiterFactoryFactory
{
    Task<ISlidingWindowRateLimiterFactory> GetFactoryAsync(string? policyName,
        CancellationToken cancellationToken = default);
}

internal class SlidingWindowRateLimiterFactoryFactory(
    IServiceProvider serviceProvider,
    IRedisSharedConnectionService redisSharedConnectionService,
    IOptions<GeneralRateLimiterOptions> options) : ISlidingWindowRateLimiterFactoryFactory
{
    public async Task<ISlidingWindowRateLimiterFactory> GetFactoryAsync(string? policyName,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseRedis)
        {
            var inMemoryFactory = serviceProvider.GetRequiredService<IInMemorySlidingWindowFactory>();
            inMemoryFactory.Initialize(policyName ?? string.Empty);
            return inMemoryFactory;
        }

        var factory = serviceProvider.GetRequiredService<IRedisSlidingWindowRateLimiterFactory>();
        factory.Initialize(policyName, await redisSharedConnectionService.GetConnectionAsync(cancellationToken));
        return factory;
    }
}