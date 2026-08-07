using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Platform.Extensions.Messages;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Factories;

public class RedisSlidingWindowFactoryTests
{
    public class GetRateLimiter
    {
        [Fact]
        public void ReturnsPartition_AfterInitialize()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Mock.Of<IHttpContextAccessor>());
            var provider = services.BuildServiceProvider();

            var redis = new Mock<IDatabase>(MockBehavior.Strict);
            var factory = new RedisSlidingWindowFactory(provider);
            factory.Initialize("policy-b", redis.Object);

            var policy = new RateLimitingPolicyOptions
            {
                Name = "policy-b",
                RedisKeyPrefix = "rl",
                WindowPermitLimit = 5,
                LimitWindowMinutes = 1,
                FailClosed = false
            };

            var partition = factory.GetRateLimiter("user:9", policy);

            Assert.Equal("user:9", partition.PartitionKey);
            using var limiter = partition.Factory(partition.PartitionKey);
            Assert.NotNull(limiter);
        }

        [Fact]
        public void Throws_WhenNotInitialized()
        {
            var factory = new RedisSlidingWindowFactory(new ServiceCollection().BuildServiceProvider());
            var policy = new RateLimitingPolicyOptions
            {
                Name = "p",
                RedisKeyPrefix = "prefix",
                WindowPermitLimit = 5,
                LimitWindowMinutes = 1,
                FailClosed = true
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                factory.GetRateLimiter("user:1", policy));

            Assert.Contains("without initialization", ex.Message);
        }

        [Fact]
        public void UsesDefaultPrefix_WhenRedisKeyPrefixBlank()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Mock.Of<IHttpContextAccessor>());
            var provider = services.BuildServiceProvider();

            var redis = new Mock<IDatabase>(MockBehavior.Strict);
            var factory = new RedisSlidingWindowFactory(provider);
            factory.Initialize("policy-c", redis.Object);

            var policy = new RateLimitingPolicyOptions
            {
                Name = "policy-c",
                RedisKeyPrefix = " ",
                WindowPermitLimit = 1,
                LimitWindowMinutes = 1,
                FailClosed = false
            };

            var partition = factory.GetRateLimiter("anon", policy);
            using var limiter = partition.Factory(partition.PartitionKey);

            Assert.NotNull(limiter);
        }
    }
}