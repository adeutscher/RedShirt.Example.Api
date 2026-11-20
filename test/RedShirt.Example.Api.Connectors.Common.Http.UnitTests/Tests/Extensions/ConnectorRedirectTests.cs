using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Connectors.Common.Http.Extensions;
using RedShirt.Example.Api.Connectors.Common.Http.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RedShirt.Example.Api.Connectors.Common.Http.UnitTests.Tests.Extensions;

/// <summary>
///     Connector clients must not follow redirects. .NET drops only the Authorization header on a redirect, so a custom
///     credential header (Foo's x-api-key) would be sent to whatever host the redirect names.
///     These tests use two loopback servers: the first answers 302 pointing at the second, which records what it receives.
/// </summary>
public class ConnectorRedirectTests
{
    private const string ApiKeyHeader = "x-api-key";
    private const string ApiKey = "secret-api-key";

    [Fact]
    public async Task ConfigureConnectorConnections_NamedClientDoesNotFollowRedirects()
    {
        var services = new ServiceCollection();
        services.Configure<RedirectTestConfiguration>(new ConfigurationBuilder().Build());
        services.AddHttpClient("pooled").ConfigureConnectorConnections<RedirectTestConfiguration>("Connectors:Test");
        using var provider = services.BuildServiceProvider();
        using var scenario = new RedirectScenario();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("pooled");

        var status = await scenario.SendAsync(client, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, status);
        Assert.Null(scenario.TargetReceived);
    }

    [Fact]
    public async Task ConfigureConnectorPrimaryHandler_NamedClientDoesNotFollowRedirects()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("token").ConfigureConnectorPrimaryHandler();
        using var provider = services.BuildServiceProvider();
        using var scenario = new RedirectScenario();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("token");

        var status = await scenario.SendAsync(client, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, status);
        Assert.Null(scenario.TargetReceived);
    }

    [Fact]
    public void CreateConnectorPrimaryHandler_DisablesAutomaticRedirects()
    {
        using var handler = ConnectorHttpClientBuilderExtensions.CreateConnectorPrimaryHandler();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task CreateConnectorPrimaryHandler_ReturnsRedirect_WithoutContactingTarget()
    {
        using var scenario = new RedirectScenario();
        using var client = new HttpClient(ConnectorHttpClientBuilderExtensions.CreateConnectorPrimaryHandler());

        var status = await scenario.SendAsync(client, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, status);
        Assert.Null(scenario.TargetReceived);
    }

    [Fact]
    public async Task DefaultHandler_FollowsRedirect_AndLeaksCustomHeader()
    {
        // Control: documents the .NET behaviour these settings guard against.
        using var scenario = new RedirectScenario();
        using var client = new HttpClient(new HttpClientHandler());

        var status = await scenario.SendAsync(client, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(scenario.TargetReceived);
        Assert.Contains($"{ApiKeyHeader}: {ApiKey}", scenario.TargetReceived, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RedirectScenario : IDisposable
    {
        private readonly TcpListener _redirecting = new(IPAddress.Loopback, 0);
        private readonly TcpListener _target = new(IPAddress.Loopback, 0);

        private string TargetUrl => $"http://127.0.0.1:{((IPEndPoint) _target.LocalEndpoint).Port}/steal";

        private static async Task<string> ServeOnceAsync(TcpListener listener, string response,
            CancellationToken cancellationToken)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            var stream = client.GetStream();
            var buffer = new byte[8192];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            return received.ToString();
        }

        public Uri RedirectingUri =>
            new($"http://127.0.0.1:{((IPEndPoint) _redirecting.LocalEndpoint).Port}/api/foo/1");

        /// <summary>Request text the redirect target received, or null if nothing connected to it.</summary>
        public string? TargetReceived { get; private set; }

        public RedirectScenario()
        {
            _redirecting.Start();
            _target.Start();
        }

        public void Dispose()
        {
            _redirecting.Stop();
            _target.Stop();
        }

        public async Task<HttpStatusCode> SendAsync(HttpClient client, CancellationToken cancellationToken)
        {
            var redirectServer = ServeOnceAsync(_redirecting,
                $"HTTP/1.1 302 Found\r\nLocation: {TargetUrl}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                cancellationToken);
            var targetServer = ServeOnceAsync(_target,
                "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, RedirectingUri);
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, ApiKey);
            using var response = await client.SendAsync(request, cancellationToken);

            await redirectServer;
            // Give a followed redirect a moment to arrive, then stop waiting.
            var finished = await Task.WhenAny(targetServer,
                Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken));
            if (finished == targetServer)
            {
                TargetReceived = await targetServer;
            }

            return response.StatusCode;
        }
    }

    private sealed class RedirectTestConfiguration : IConnectorConnectionConfiguration
    {
        public bool ClientPerRequest { get; init; }
        public int? HandlerLifetimeSeconds { get; init; }
        public int? MaxConnectionsPerServer { get; init; }
    }
}