using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using RedShirt.Example.Api.Common.RateLimiting.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    public class ConsiderAddingRateLimitingPolicies
    {
        private static IConfigurationRoot BuildEnabledConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DisableRateLimiting"] = "false",
                    ["RateLimiting:UseRedis"] = "false",
                    ["RateLimiting:DefaultPolicyName"] = "default",
                    ["RateLimiting:Policies:default:Name"] = "default",
                    ["RateLimiting:Policies:default:RedisKeyPrefix"] = "rl",
                    ["RateLimiting:Policies:default:WindowPermitLimit"] = "10",
                    ["RateLimiting:Policies:default:LimitWindowMinutes"] = "1",
                    ["RateLimiting:Policies:default:FailClosed"] = "false",
                    ["Common:Redis:ConnectionStringPath"] = "secrets/redis"
                })
                .Build();
        }

        [Fact]
        public void RegistersExpectedServices_WhenEnabled()
        {
            var configuration = BuildEnabledConfiguration();
            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddLogging();

            services.ConsiderAddingRateLimitingPolicies(configuration);

            using var provider = services.BuildServiceProvider();

            Assert.IsType<PartitionKeyResolverService>(
                provider.GetRequiredService<IPartitionKeyResolverService>());
            Assert.IsType<SlidingWindowRateLimiterFactoryFactory>(
                provider.GetRequiredService<ISlidingWindowRateLimiterFactoryFactory>());
            Assert.NotNull(provider.GetRequiredService<IHttpContextAccessor>());

            var options = provider.GetRequiredService<IOptions<GeneralRateLimiterOptions>>().Value;
            Assert.False(options.DisableRateLimiting);
            Assert.Equal("default", options.DefaultPolicyName);
        }

        [Fact]
        public void RegistersServicesAsExpectedLifetimes()
        {
            var configuration = BuildEnabledConfiguration();
            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);

            services.ConsiderAddingRateLimitingPolicies(configuration);

            Assert.Contains(services,
                d => d.ServiceType == typeof(IPartitionKeyResolverService)
                     && d.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services,
                d => d.ServiceType == typeof(ISlidingWindowRateLimiterFactoryFactory)
                     && d.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services,
                d => d.ServiceType == typeof(IInMemorySlidingWindowFactory)
                     && d.Lifetime == ServiceLifetime.Transient);
            Assert.Contains(services,
                d => d.ServiceType == typeof(IRedisSlidingWindowRateLimiterFactory)
                     && d.Lifetime == ServiceLifetime.Transient);
        }

        [Fact]
        public void ReturnsSameCollection_WithoutRegistering_WhenDisabled()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DisableRateLimiting"] = "true"
                })
                .Build();
            var services = new ServiceCollection();

            var result = services.ConsiderAddingRateLimitingPolicies(configuration);

            Assert.Same(services, result);
            Assert.Empty(services);
        }
    }
}