using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Redis.Factories;
using RedShirt.Example.Api.Common.Redis.Services;
using RedShirt.Example.Api.Common.Services;

namespace RedShirt.Example.Api.Common.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisImplementations(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            // Supporting
            .AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>()
            .Configure<RedisConnectionFactory.ConfigurationModel>(configuration.GetSection("Common:Redis"))
            .AddSingleton<IRedisSharedConnectionService, RedisSharedConnectionService>()
            // Main
            .AddSingleton<IDataCacheService, RedisCacheService>()
            .AddSingleton<IAbstractedLockService, RedisLockService>();
    }
}