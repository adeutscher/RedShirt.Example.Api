using Microsoft.AspNetCore.Http;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using RedShirt.Example.Api.Common.RateLimiting.RateLimiters;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Factories;

public class InMemorySlidingWindowFactoryTests
{
    public class GetRateLimiter
    {
        [Fact]
        public void ReturnsPartition_UsingPolicyOptions()
        {
            var factory = new InMemorySlidingWindowFactory(Mock.Of<IHttpContextAccessor>());
            factory.Initialize("policy-a");

            var policy = new RateLimitingPolicyOptions
            {
                Name = "policy-a",
                RedisKeyPrefix = "p",
                WindowPermitLimit = 7,
                LimitWindowMinutes = 2,
                FailClosed = false
            };

            var partition = factory.GetRateLimiter("user:1", policy);

            Assert.Equal("user:1:policy-a", partition.PartitionKey);
            using var limiter = partition.Factory(partition.PartitionKey);
            Assert.IsType<InMemorySlidingWindowRateLimiter>(limiter);
        }
    }
}