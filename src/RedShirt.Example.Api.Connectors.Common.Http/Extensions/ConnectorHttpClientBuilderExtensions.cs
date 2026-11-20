using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Common.Http.Models;

namespace RedShirt.Example.Api.Connectors.Common.Http.Extensions;

public static class ConnectorHttpClientBuilderExtensions
{
    public const int DefaultHandlerLifetimeSeconds = 120;

    /// <summary>
    ///     Applies <see cref="IConnectorConnectionConfiguration.HandlerLifetimeSeconds" /> and
    ///     <see cref="IConnectorConnectionConfiguration.MaxConnectionsPerServer" /> from
    ///     <typeparamref name="TConfiguration" /> to the pooled named client.
    /// </summary>
    /// <param name="builder">
    ///     The connector's named client.
    /// </param>
    /// <param name="sectionName">
    ///     Configuration section bound to <typeparamref name="TConfiguration" />, named in validation errors.
    /// </param>
    public static IHttpClientBuilder ConfigureConnectorConnections<TConfiguration>(this IHttpClientBuilder builder,
        string sectionName)
        where TConfiguration : class, IConnectorConnectionConfiguration
    {
        builder.ConfigurePrimaryHttpMessageHandler(provider =>
            CreatePooledPrimaryHandler(provider.GetRequiredService<IOptions<TConfiguration>>().Value));

        builder.Services.AddOptions<HttpClientFactoryOptions>(builder.Name)
            .Configure<IOptions<TConfiguration>>((factoryOptions, connectorOptions) =>
            {
                var seconds = connectorOptions.Value.HandlerLifetimeSeconds ?? DefaultHandlerLifetimeSeconds;
                if (seconds < 1)
                {
                    throw new InvalidOperationException(
                        $"{sectionName}:HandlerLifetimeSeconds is {seconds}. It must be at least 1.");
                }

                factoryOptions.HandlerLifetime = TimeSpan.FromSeconds(seconds);
            });

        return builder;
    }

    /// <summary>
    ///     Uses <see cref="CreateConnectorPrimaryHandler" /> as the named client's primary handler. For connector clients
    ///     that do not need <see cref="ConfigureConnectorConnections{TConfiguration}" /> (for example an OAuth token
    ///     client).
    /// </summary>
    public static IHttpClientBuilder ConfigureConnectorPrimaryHandler(this IHttpClientBuilder builder)
    {
        return builder.ConfigurePrimaryHttpMessageHandler(CreateConnectorPrimaryHandler);
    }

    /// <summary>
    ///     Creates the primary handler every connector <see cref="HttpClient" /> should use.
    ///     <para>
    ///         Automatic redirects are off. Connectors put credentials on each request (an <c>x-api-key</c> header, an
    ///         OAuth client secret in a token request body). When a redirect is followed, .NET drops only the
    ///         <c>Authorization</c> header, so a custom header goes to the redirect target as-is, and a 307 or 308 resends
    ///         the body. A redirect from a dependency instead surfaces as an unsuccessful status code.
    ///     </para>
    /// </summary>
    public static HttpClientHandler CreateConnectorPrimaryHandler()
    {
        return new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
    }

    /// <summary>
    ///     The connection cap is only set when one is configured, so platforms that do not support it are unaffected
    ///     by default.
    /// </summary>
    internal static HttpMessageHandler CreatePooledPrimaryHandler(IConnectorConnectionConfiguration configuration)
    {
        var handler = CreateConnectorPrimaryHandler();
        if (configuration.MaxConnectionsPerServer is > 0 and var maxConnectionsPerServer)
        {
            handler.MaxConnectionsPerServer = maxConnectionsPerServer;
        }

        return handler;
    }
}