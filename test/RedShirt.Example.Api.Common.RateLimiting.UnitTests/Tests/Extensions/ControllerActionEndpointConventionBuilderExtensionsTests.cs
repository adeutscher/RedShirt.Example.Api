using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Extensions;

public class ControllerActionEndpointConventionBuilderExtensionsTests
{
    public class ConsiderRequiringRateLimiting
    {
        [Fact]
        public void ReturnsSameBuilder_WhenDefaultPolicyNameConfigured()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DefaultPolicyName"] = "default"
                })
                .Build();

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Services.AddRateLimiter(_ => { });
            var app = builder.Build();
            var endpoints = app.MapControllers();

            var result = endpoints.ConsiderRequiringRateLimiting(configuration);

            Assert.Same(endpoints, result);
        }

        [Fact]
        public void ReturnsSameBuilder_WhenDefaultPolicyNameMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:DisableRateLimiting"] = "false"
                })
                .Build();

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            var app = builder.Build();
            var endpoints = app.MapControllers();

            var result = endpoints.ConsiderRequiringRateLimiting(configuration);

            Assert.Same(endpoints, result);
        }
    }
}