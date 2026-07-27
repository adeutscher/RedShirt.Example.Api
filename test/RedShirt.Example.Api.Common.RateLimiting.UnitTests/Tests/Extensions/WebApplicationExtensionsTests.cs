using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Configuration;
using RedShirt.Example.Api.Common.RateLimiting.Constants;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Factories;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Extensions;

public class WebApplicationExtensionsTests
{
    public class ConsiderAddingRateLimitParts
    {
        [Fact]
        public async Task DoesNotResolveFactory_WhenDisabledInConfiguration()
        {
            var factoryFactory = new Mock<ISlidingWindowRateLimiterFactoryFactory>(MockBehavior.Strict);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:DisableRateLimiting"] = "true",
                ["RateLimiting:UseRedis"] = "false"
            });
            builder.Services.AddRouting();
            builder.Services.AddSingleton(factoryFactory.Object);

            await using var app = builder.Build();
            app.UseRouting();
            app.ConsiderAddingRateLimitParts();
            app.MapGet("/ping", () => Results.NoContent());

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/ping", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            factoryFactory.VerifyNoOtherCalls();
        }

        [Fact]
        public void ReturnsEarly_WithoutRequiringOptions_WhenDisabledInConfiguration()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:DisableRateLimiting"] = "true",
                ["RateLimiting:UseRedis"] = "false"
            });
            // Intentionally omit IOptions<GeneralRateLimiterOptions> — early return must not resolve it.
            var app = builder.Build();

            var result = app.ConsiderAddingRateLimitParts();

            Assert.Same(app, result);
        }

        [Fact]
        public async Task SkipsFactoryLookup_WhenEndpointHasDisableRateLimiting()
        {
            var factoryFactory = new Mock<ISlidingWindowRateLimiterFactoryFactory>(MockBehavior.Strict);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddRateLimiter(_ => { });
            builder.Services.AddSingleton(factoryFactory.Object);
            builder.Services.AddSingleton(Options.Create(new GeneralRateLimiterOptions
            {
                DisableRateLimiting = false,
                UseRedis = false,
                DefaultPolicyName = "default"
            }));

            await using var app = builder.Build();
            app.UseRouting();
            app.ConsiderAddingRateLimitParts();
            app.MapGet("/open", () => Results.NoContent()).DisableRateLimiting();

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/open", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            factoryFactory.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task StoresFactoryInHttpContextItems_BeforeEndpoint()
        {
            var factory = new Mock<ISlidingWindowRateLimiterFactory>(MockBehavior.Strict);
            var factoryFactory = new Mock<ISlidingWindowRateLimiterFactoryFactory>(MockBehavior.Strict);
            factoryFactory
                .Setup(f => f.GetFactoryAsync("default", It.IsAny<CancellationToken>()))
                .ReturnsAsync(factory.Object);

            object? captured = null;

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("default", _ => RateLimitPartition.GetNoLimiter("n"));
            });
            builder.Services.AddSingleton(factoryFactory.Object);
            builder.Services.AddSingleton(Options.Create(new GeneralRateLimiterOptions
            {
                DisableRateLimiting = false,
                UseRedis = false,
                DefaultPolicyName = "default"
            }));

            await using var app = builder.Build();
            app.UseRouting();
            app.ConsiderAddingRateLimitParts();
            app.Use(async (context, next) =>
            {
                context.Items.TryGetValue(HttpContextConstants.FactoryItemKey, out captured);
                await next();
            });
            app.MapGet("/ping", () => Results.NoContent()).RequireRateLimiting("default");

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/ping", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            Assert.Same(factory.Object, captured);
            factoryFactory.Verify(f => f.GetFactoryAsync("default", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UsesPolicyNameFromEnableAttribute_WhenPresent()
        {
            var factory = new Mock<ISlidingWindowRateLimiterFactory>(MockBehavior.Strict);
            var factoryFactory = new Mock<ISlidingWindowRateLimiterFactoryFactory>(MockBehavior.Strict);
            factoryFactory
                .Setup(f => f.GetFactoryAsync("named-policy", It.IsAny<CancellationToken>()))
                .ReturnsAsync(factory.Object);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("named-policy", _ => RateLimitPartition.GetNoLimiter("n"));
            });
            builder.Services.AddSingleton(factoryFactory.Object);
            builder.Services.AddSingleton(Options.Create(new GeneralRateLimiterOptions
            {
                DisableRateLimiting = false,
                UseRedis = false,
                DefaultPolicyName = "default"
            }));

            await using var app = builder.Build();
            app.UseRouting();
            app.ConsiderAddingRateLimitParts();
            app.MapGet("/named", () => Results.NoContent()).RequireRateLimiting("named-policy");

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/named", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            factoryFactory.Verify(f => f.GetFactoryAsync("named-policy", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}