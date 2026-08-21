using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Extensions;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class WebApplicationExtensionsTests
{
    public class ConsiderAddingPathBase
    {
        private static Dictionary<string, string?> ConfigurationWithPathBase(string? pathBase)
        {
            var values = new Dictionary<string, string?>();
            if (pathBase is not null)
            {
                values["API:PathBase"] = pathBase;
            }

            return values;
        }

        [Fact]
        public async Task DoesNotSetPathBase_WhenApiSectionMissing()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();

            string? capturedPathBase = null;

            await using var app = builder.Build();
            app.ConsiderAddingPathBase();
            app.UseRouting();
            app.MapGet("/ping", (HttpContext context) =>
            {
                capturedPathBase = context.Request.PathBase.Value;
                return Results.NoContent();
            });

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/ping", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            Assert.True(string.IsNullOrEmpty(capturedPathBase));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DoesNotSetPathBase_WhenPathBaseMissingOrWhitespace(string? pathBase)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(ConfigurationWithPathBase(pathBase));
            builder.Services.AddRouting();

            string? capturedPathBase = null;
            string? capturedPath = null;

            await using var app = builder.Build();
            app.ConsiderAddingPathBase();
            app.UseRouting();
            app.MapGet("/ping", (HttpContext context) =>
            {
                capturedPathBase = context.Request.PathBase.Value;
                capturedPath = context.Request.Path.Value;
                return Results.NoContent();
            });

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri("/ping", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            Assert.True(string.IsNullOrEmpty(capturedPathBase));
            Assert.Equal("/ping", capturedPath);
        }

        [Fact]
        public void ReturnsSameApplicationInstance()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var app = builder.Build();

            var result = app.ConsiderAddingPathBase();

            Assert.Same(app, result);
        }

        [Fact]
        public async Task SetsPathBase_WhenConfigured()
        {
            const string pathBase = "/example";

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(ConfigurationWithPathBase(pathBase));
            builder.Services.AddRouting();

            string? capturedPathBase = null;
            string? capturedPath = null;

            await using var app = builder.Build();
            app.ConsiderAddingPathBase();
            app.UseRouting();
            app.MapGet("/ping", (HttpContext context) =>
            {
                capturedPathBase = context.Request.PathBase.Value;
                capturedPath = context.Request.Path.Value;
                return Results.NoContent();
            });

            await app.StartAsync(TestContext.Current.CancellationToken);
            var client = app.GetTestClient();
            var response = await client.GetAsync(new Uri($"{pathBase}/ping", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status204NoContent, (int) response.StatusCode);
            Assert.Equal(pathBase, capturedPathBase);
            Assert.Equal("/ping", capturedPath);
        }
    }
}