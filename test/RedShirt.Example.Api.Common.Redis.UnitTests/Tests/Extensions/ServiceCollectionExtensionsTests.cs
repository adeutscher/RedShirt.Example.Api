using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.Redis.Extensions;
using RedShirt.Example.Api.Common.Redis.Factories;
using RedShirt.Example.Api.Common.Redis.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using RedShirt.Example.Api.Common.Services;

namespace RedShirt.Example.Api.Common.Redis.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    public class AddRedisImplementations
    {
        [Fact]
        public void BindsConnectionStringPath_FromCommonRedisSection()
        {
            var connectionStringPath = Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = connectionStringPath
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddRedisImplementations(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RedisConnectionFactory.ConfigurationModel>>();

            Assert.Equal(connectionStringPath, options.Value.ConnectionStringPath);
        }

        [Fact]
        public void RegistersRedisCacheService_AsIDataCacheServiceSingleton()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = Guid.NewGuid().ToString("N")
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddRedisImplementations(configuration);

            using var provider = services.BuildServiceProvider();

            var first = provider.GetRequiredService<IDataCacheService>();
            var second = provider.GetRequiredService<IDataCacheService>();

            Assert.IsType<RedisCacheService>(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void RegistersRedisConnectionFactory_AsSingleton()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = Guid.NewGuid().ToString("N")
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddRedisImplementations(configuration);

            using var provider = services.BuildServiceProvider();

            var first = provider.GetRequiredService<IRedisConnectionFactory>();
            var second = provider.GetRequiredService<IRedisConnectionFactory>();

            Assert.IsType<RedisConnectionFactory>(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void RegistersRedisLockService_AsIAbstractedLockServiceSingleton()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = Guid.NewGuid().ToString("N")
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddRedisImplementations(configuration);

            using var provider = services.BuildServiceProvider();

            var first = provider.GetRequiredService<IAbstractedLockService>();
            var second = provider.GetRequiredService<IAbstractedLockService>();

            Assert.IsType<RedisLockService>(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void RegistersRedisSharedConnectionService_AsSingleton()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = Guid.NewGuid().ToString("N")
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ISecretManagerCacheService>(MockBehavior.Strict).Object);
            services.AddRedisImplementations(configuration);

            using var provider = services.BuildServiceProvider();

            var first = provider.GetRequiredService<IRedisSharedConnectionService>();
            var second = provider.GetRequiredService<IRedisSharedConnectionService>();

            Assert.IsType<RedisSharedConnectionService>(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void ReturnsSameServiceCollection()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:Redis:ConnectionStringPath"] = Guid.NewGuid().ToString("N")
                })
                .Build();
            var services = new ServiceCollection();

            var result = services.AddRedisImplementations(configuration);

            Assert.Same(services, result);
        }
    }
}