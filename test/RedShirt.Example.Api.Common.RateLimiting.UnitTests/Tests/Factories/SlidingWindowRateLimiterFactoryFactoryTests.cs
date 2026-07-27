using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using RedShirt.Example.Api.Common.Redis.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Factories;

public class SlidingWindowRateLimiterFactoryFactoryTests
{
    public class GetFactoryAsync
    {
        [Fact]
        public async Task InitializesRedisFactory_WhenUseRedisTrue()
        {
            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            var redisFactory = new Mock<IRedisSlidingWindowRateLimiterFactory>(MockBehavior.Strict);
            redisFactory
                .Setup(f => f.Initialize("policy-x", connection.Object));

            var redisConnection = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            redisConnection
                .Setup(s => s.GetConnectionAsync(TestContext.Current.CancellationToken))
                .ReturnsAsync(connection.Object);

            var services = new ServiceCollection();
            services.AddSingleton(redisFactory.Object);
            var provider = services.BuildServiceProvider();

            var options = Options.Create(new GeneralRateLimiterOptions
            {
                DisableRateLimiting = false,
                UseRedis = true,
                DefaultPolicyName = "default"
            });

            var factoryFactory = new SlidingWindowRateLimiterFactoryFactory(
                provider, redisConnection.Object, options);

            var factory = await factoryFactory.GetFactoryAsync("policy-x", TestContext.Current.CancellationToken);

            Assert.Same(redisFactory.Object, factory);
            redisFactory.Verify(f => f.Initialize("policy-x", connection.Object), Times.Once);
            redisConnection.Verify(s => s.GetConnectionAsync(TestContext.Current.CancellationToken), Times.Once);
        }

        [Fact]
        public async Task ReturnsInMemoryFactory_WhenUseRedisFalse()
        {
            var inMemory = new Mock<IInMemorySlidingWindowFactory>(MockBehavior.Strict);
            inMemory.Setup(f => f.Initialize("policy"));
            var services = new ServiceCollection();
            services.AddSingleton(inMemory.Object);
            var provider = services.BuildServiceProvider();

            var redisConnection = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            var options = Options.Create(new GeneralRateLimiterOptions
            {
                DisableRateLimiting = false,
                UseRedis = false,
                DefaultPolicyName = "default"
            });

            var factoryFactory = new SlidingWindowRateLimiterFactoryFactory(
                provider, redisConnection.Object, options);

            var factory = await factoryFactory.GetFactoryAsync("policy", TestContext.Current.CancellationToken);

            Assert.Same(inMemory.Object, factory);
            inMemory.Verify(f => f.Initialize("policy"), Times.Once);
            redisConnection.VerifyNoOtherCalls();
        }
    }
}