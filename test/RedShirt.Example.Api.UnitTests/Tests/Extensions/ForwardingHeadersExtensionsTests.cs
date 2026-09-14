using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Extensions;
using System.Net;
using IPNetwork = System.Net.IPNetwork;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class ForwardingHeadersExtensionsTests
{
    private const string ConfigurationSectionName = "HeaderForwarding";

    private static IConfigurationRoot CreateConfiguration(
        bool? respectHeaders = null,
        params string[] trustedNetworks)
    {
        var values = new Dictionary<string, string?>();
        if (respectHeaders is not null)
        {
            values[$"{ConfigurationSectionName}:RespectHeaders"] = respectHeaders.Value.ToString();
        }

        for (var index = 0; index < trustedNetworks.Length; index++)
        {
            values[$"{ConfigurationSectionName}:TrustedNetworks:{index}"] = trustedNetworks[index];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void ConsiderConfiguringForwardingHeaders_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(respectHeaders: true, "10.0.0.0/8");

        var result = services.ConsiderConfiguringForwardingHeaders(configuration);

        Assert.Same(services, result);
    }

    [Fact]
    public void ConsiderConfiguringForwardingHeaders_DoesNotRegisterOptionsWhenDisabled()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(respectHeaders: false);

        services.ConsiderConfiguringForwardingHeaders(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IOptions<ForwardedHeadersOptions>>());
    }

    [Fact]
    public void ConsiderConfiguringForwardingHeaders_ConfiguresOptionsWhenEnabled()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            respectHeaders: true,
            "10.0.0.0/8",
            "not-a-cidr",
            " 172.16.0.0/12 ",
            "2001:db8::/32");

        services.ConsiderConfiguringForwardingHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost
            | ForwardedHeaders.XForwardedPrefix,
            options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Equal(3, options.KnownIPNetworks.Count);
        Assert.Contains(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8), options.KnownIPNetworks);
        Assert.Contains(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12), options.KnownIPNetworks);
        Assert.Contains(new IPNetwork(IPAddress.Parse("2001:db8::"), 32), options.KnownIPNetworks);
    }

    [Fact]
    public void ConsiderConfiguringForwardingHeaders_UsesDefaultsWhenSectionMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.ConsiderConfiguringForwardingHeaders(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IOptions<ForwardedHeadersOptions>>());
    }

    [Fact]
    public void ConsiderUsingForwardedHeaders_ReturnsSameApplicationInstance()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        var result = app.ConsiderUsingForwardedHeaders();

        Assert.Same(app, result);
    }

    [Fact]
    public async Task ConsiderUsingForwardedHeaders_DoesNotApplyHeadersWhenDisabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(CreateConfiguration(respectHeaders: false)
            .AsEnumerable()
            .ToDictionary(entry => entry.Key, entry => entry.Value));
        builder.Services.AddRouting();

        string? capturedScheme = null;

        await using var app = builder.Build();
        app.ConsiderUsingForwardedHeaders();
        app.UseRouting();
        app.MapGet("/", (HttpRequest request) =>
        {
            capturedScheme = request.Scheme;
            return Results.NoContent();
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status204NoContent, (int)response.StatusCode);
        Assert.Equal("http", capturedScheme);
    }

    [Fact]
    public async Task ConsiderUsingForwardedHeaders_AppliesHeadersWhenEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(CreateConfiguration(respectHeaders: true, "0.0.0.0/0")
            .AsEnumerable()
            .ToDictionary(entry => entry.Key, entry => entry.Value));
        builder.Services
            .ConsiderConfiguringForwardingHeaders((IConfigurationRoot)builder.Configuration)
            .AddRouting();

        string? capturedScheme = null;

        await using var app = builder.Build();
        app.ConsiderUsingForwardedHeaders();
        app.UseRouting();
        app.MapGet("/", (HttpRequest request) =>
        {
            capturedScheme = request.Scheme;
            return Results.NoContent();
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status204NoContent, (int)response.StatusCode);
        Assert.Equal("https", capturedScheme);
    }
}
