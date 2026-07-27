using Microsoft.Extensions.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Utility;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Utility;

public class RateLimitPolicyRetrieverTests
{
    public class GetPolicies
    {
        [Fact]
        public void ReturnsEmpty_WhenRateLimitingDisabled()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DisableRateLimiting"] = "true",
                    ["RateLimiting:UseRedis"] = "false",
                    ["RateLimiting:Policies:raw-key:Name"] = "named-policy",
                    ["RateLimiting:Policies:raw-key:RedisKeyPrefix"] = "prefix",
                    ["RateLimiting:Policies:raw-key:WindowPermitLimit"] = "10",
                    ["RateLimiting:Policies:raw-key:LimitWindowMinutes"] = "1",
                    ["RateLimiting:Policies:raw-key:FailClosed"] = "false"
                })
                .Build();

            var policies = RateLimitPolicyRetriever.GetPolicies(configuration);

            Assert.Empty(policies);
        }

        [Fact]
        public void Throws_WhenNoPoliciesConfigured()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DisableRateLimiting"] = "false"
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                RateLimitPolicyRetriever.GetPolicies(configuration));

            Assert.Equal("No rate-limit policies have been configured.", ex.Message);
        }

        [Fact]
        public void UsesDictionaryKey_WhenPolicyNameBlank()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Policies:fallback-key:Name"] = " ",
                    ["RateLimiting:Policies:fallback-key:RedisKeyPrefix"] = "prefix",
                    ["RateLimiting:Policies:fallback-key:WindowPermitLimit"] = "5",
                    ["RateLimiting:Policies:fallback-key:LimitWindowMinutes"] = "2",
                    ["RateLimiting:Policies:fallback-key:FailClosed"] = "true"
                })
                .Build();

            var policies = RateLimitPolicyRetriever.GetPolicies(configuration);

            Assert.True(policies.ContainsKey("fallback-key"));
            Assert.True(policies["fallback-key"].FailClosed);
        }

        [Fact]
        public void UsesPolicyName_WhenConfigured()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Policies:raw-key:Name"] = "named-policy",
                    ["RateLimiting:Policies:raw-key:RedisKeyPrefix"] = "prefix",
                    ["RateLimiting:Policies:raw-key:WindowPermitLimit"] = "10",
                    ["RateLimiting:Policies:raw-key:LimitWindowMinutes"] = "1",
                    ["RateLimiting:Policies:raw-key:FailClosed"] = "false"
                })
                .Build();

            var policies = RateLimitPolicyRetriever.GetPolicies(configuration);

            Assert.True(policies.ContainsKey("named-policy"));
            Assert.False(policies.ContainsKey("raw-key"));
            Assert.Equal("named-policy", policies["named-policy"].Name);
            Assert.Equal(10, policies["named-policy"].WindowPermitLimit);
        }
    }
}