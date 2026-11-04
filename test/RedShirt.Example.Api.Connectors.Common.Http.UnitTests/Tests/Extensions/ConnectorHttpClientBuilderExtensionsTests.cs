using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Common.Http.Extensions;
using RedShirt.Example.Api.Connectors.Common.Http.Models;

namespace RedShirt.Example.Api.Connectors.Common.Http.UnitTests.Tests.Extensions;

public class ConnectorHttpClientBuilderExtensionsTests
{
    private const string ClientName = "Test";
    private const string SectionName = "Connectors:Test";

    private static IOptionsMonitor<HttpClientFactoryOptions> CreateFactoryOptions(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.Configure<TestConfiguration>(configuration.GetSection(SectionName));
        services.AddHttpClient(ClientName).ConfigureConnectorConnections<TestConfiguration>(SectionName);
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
    }

    [Fact]
    public void ConfigureConnectorConnections_AppliesConfiguredHandlerLifetime()
    {
        var options = CreateFactoryOptions(new Dictionary<string, string?>
        {
            [$"{SectionName}:HandlerLifetimeSeconds"] = "15"
        });

        Assert.Equal(TimeSpan.FromSeconds(15), options.Get(ClientName).HandlerLifetime);
    }

    [Fact]
    public void ConfigureConnectorConnections_DefaultsHandlerLifetimeTo120Seconds()
    {
        var options = CreateFactoryOptions([]);

        Assert.Equal(TimeSpan.FromSeconds(120), options.Get(ClientName).HandlerLifetime);
    }

    [Fact]
    public void ConfigureConnectorConnections_LeavesOtherClientsAlone()
    {
        var options = CreateFactoryOptions(new Dictionary<string, string?>
        {
            [$"{SectionName}:HandlerLifetimeSeconds"] = "0"
        });

        Assert.Equal(TimeSpan.FromMinutes(2), options.Get("Other").HandlerLifetime);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ConfigureConnectorConnections_Throws_WhenHandlerLifetimeIsBelowOneSecond(string seconds)
    {
        var options = CreateFactoryOptions(new Dictionary<string, string?>
        {
            [$"{SectionName}:HandlerLifetimeSeconds"] = seconds
        });

        var exception = Assert.Throws<InvalidOperationException>(() => options.Get(ClientName));
        Assert.Contains($"{SectionName}:HandlerLifetimeSeconds", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreatePooledPrimaryHandler_LeavesConnectionCapUnset_WhenNotPositive(int? maxConnectionsPerServer)
    {
        using var handler = Assert.IsType<HttpClientHandler>(
            ConnectorHttpClientBuilderExtensions.CreatePooledPrimaryHandler(
                new TestConfiguration
                {
                    ClientPerRequest = false,
                    HandlerLifetimeSeconds = null,
                    MaxConnectionsPerServer = maxConnectionsPerServer
                }));

        Assert.Equal(int.MaxValue, handler.MaxConnectionsPerServer);
    }

    [Fact]
    public void CreatePooledPrimaryHandler_SetsConnectionCap_WhenPositive()
    {
        using var handler = Assert.IsType<HttpClientHandler>(
            ConnectorHttpClientBuilderExtensions.CreatePooledPrimaryHandler(
                new TestConfiguration
                {
                    ClientPerRequest = false,
                    HandlerLifetimeSeconds = null,
                    MaxConnectionsPerServer = 4
                }));

        Assert.Equal(4, handler.MaxConnectionsPerServer);
    }

    private sealed class TestConfiguration : IConnectorConnectionConfiguration
    {
        public required bool ClientPerRequest { get; init; }

        public required int? HandlerLifetimeSeconds { get; init; }

        public required int? MaxConnectionsPerServer { get; init; }
    }
}