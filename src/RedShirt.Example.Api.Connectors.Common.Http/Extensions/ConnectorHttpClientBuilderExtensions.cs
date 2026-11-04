using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Common.Http.Models;

namespace RedShirt.Example.Api.Connectors.Common.Http.Extensions;

public static class ConnectorHttpClientBuilderExtensions
{
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
                var seconds = connectorOptions.Value.HandlerLifetimeSeconds;
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
    ///     The connection cap is only set when one is configured, so platforms that do not support it are unaffected
    ///     by default.
    /// </summary>
    internal static HttpMessageHandler CreatePooledPrimaryHandler(IConnectorConnectionConfiguration configuration)
    {
        var handler = new HttpClientHandler();
        if (configuration.MaxConnectionsPerServer > 0)
        {
            handler.MaxConnectionsPerServer = configuration.MaxConnectionsPerServer;
        }

        return handler;
    }
}