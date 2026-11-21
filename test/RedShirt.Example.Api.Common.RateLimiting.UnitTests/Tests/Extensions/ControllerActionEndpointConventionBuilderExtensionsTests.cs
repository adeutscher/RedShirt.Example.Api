using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.UnitTests.TestControllers;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Extensions;

public class ControllerActionEndpointConventionBuilderExtensionsTests
{
    public class ConsiderRequiringRateLimiting
    {
        private static IConfiguration DefaultPolicyConfiguration(string? defaultPolicyName)
        {
            var values = new Dictionary<string, string?>
            {
                ["RateLimiting:DisableRateLimiting"] = "false"
            };
            if (defaultPolicyName is not null)
            {
                values["RateLimiting:DefaultPolicyName"] = defaultPolicyName;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        /// <summary>
        ///     Maps the test controllers with the convention applied and returns the built endpoints by route.
        /// </summary>
        private static Dictionary<string, Endpoint> BuildEndpoints(string? defaultPolicyName)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(NoPolicyConventionTestController).Assembly);
            var app = builder.Build();

            app.MapControllers().ConsiderRequiringRateLimiting(DefaultPolicyConfiguration(defaultPolicyName));

            return ((IEndpointRouteBuilder) app).DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.StartsWith("convention-test/") == true)
                .ToDictionary(e => e.RoutePattern.RawText!, e => (Endpoint) e);
        }

        /// <summary>
        ///     The policy the rate limiter applies. ASP.NET Core reads the last EnableRateLimitingAttribute.
        /// </summary>
        private static string? EffectivePolicy(Endpoint endpoint)
        {
            return endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        }

        [Fact]
        public void ReturnsSameBuilder_WhenDefaultPolicyNameConfigured()
        {
            var configuration = DefaultPolicyConfiguration("default");

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
            var configuration = DefaultPolicyConfiguration(null);

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            var app = builder.Build();
            var endpoints = app.MapControllers();

            var result = endpoints.ConsiderRequiringRateLimiting(configuration);

            Assert.Same(endpoints, result);
        }

        [Fact]
        public void AppliesDefaultPolicy_WhenEndpointNamesNone()
        {
            var endpoints = BuildEndpoints(ConventionTestPolicies.Default);

            Assert.Equal(ConventionTestPolicies.Default, EffectivePolicy(endpoints["convention-test/no-policy"]));
        }

        [Fact]
        public void KeepsControllerPolicy_OverDefaultPolicy()
        {
            var endpoints = BuildEndpoints(ConventionTestPolicies.Default);

            Assert.Equal(ConventionTestPolicies.Controller,
                EffectivePolicy(endpoints["convention-test/controller-policy"]));
        }

        [Fact]
        public void KeepsActionPolicy_OverControllerAndDefaultPolicies()
        {
            var endpoints = BuildEndpoints(ConventionTestPolicies.Default);

            Assert.Equal(ConventionTestPolicies.Action,
                EffectivePolicy(endpoints["convention-test/controller-policy/action-policy"]));
        }

        [Theory]
        [InlineData("convention-test/disabled")]
        [InlineData("convention-test/controller-policy/disabled")]
        public void AddsNoDefaultPolicy_WhenRateLimitingDisabled(string route)
        {
            var endpoint = BuildEndpoints(ConventionTestPolicies.Default)[route];

            Assert.NotNull(endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
            Assert.DoesNotContain(endpoint.Metadata.OfType<EnableRateLimitingAttribute>(),
                a => a.PolicyName == ConventionTestPolicies.Default);
        }

        [Fact]
        public void AddsNoPolicy_WhenDefaultPolicyNameMissing()
        {
            var endpoints = BuildEndpoints(null);

            Assert.Null(EffectivePolicy(endpoints["convention-test/no-policy"]));
            Assert.Equal(ConventionTestPolicies.Controller,
                EffectivePolicy(endpoints["convention-test/controller-policy"]));
        }
    }
}
