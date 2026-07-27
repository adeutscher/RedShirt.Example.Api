using RedShirt.Example.Api.Common.RateLimiting.Configuration;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Configuration;

public class RateLimitingPolicyOptionsTests
{
    public class Window
    {
        [Fact]
        public void UsesLimitWindowMinutes_NotPermitLimit()
        {
            var options = new RateLimitingPolicyOptions
            {
                Name = "p",
                RedisKeyPrefix = "r",
                WindowPermitLimit = 100,
                LimitWindowMinutes = 3,
                FailClosed = false
            };

            Assert.Equal(TimeSpan.FromMinutes(3), options.Window);
        }
    }
}