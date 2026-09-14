using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace RedShirt.Example.Api.Extensions;

public static class ForwardingHeadersExtensions
{
    /// <summary>
    ///     Parses a CIDR string (for example <c>172.16.0.0/12</c>) into an <see cref="IPNetwork" />.
    /// </summary>
    /// <param name="input">CIDR string</param>
    /// <param name="network">The parsed network when successful.</param>
    /// <returns><c>true</c> if successfully parsed, otherwise <c>false</c>.</returns>
    private static bool TryParseCidrAddressToNetwork(this string input, out IPNetwork network)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            return IPNetwork.TryParse(input.AsSpan().Trim(), out network);
        }

        network = default;
        return false;
    }

    private static ConfigurationModel GetConfigurationModel(IConfiguration configuration)
    {
        return configuration
            .GetSection("HeaderForwarding")
            .Get<ConfigurationModel>() ?? new ConfigurationModel
        {
            RespectHeaders = false,
            TrustedNetworks = []
        };
    }

    public static IServiceCollection ConsiderConfiguringForwardingHeaders(this IServiceCollection serviceCollection,
        IConfigurationRoot configuration)
    {
        var configurationModel = GetConfigurationModel(configuration);

        if (!configurationModel.RespectHeaders)
        {
            // Not enabled, immediately return
            return serviceCollection;
        }

        return serviceCollection.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost
                | ForwardedHeaders.XForwardedPrefix;

            // TCP peer must be a trusted proxy for forwarding headers, so add range to known network.
            // A known network is a CIDR whose TCP peers ASP.NET treats as trusted proxies for X-Forwarded-* headers.
            // Example for Cloudflare proxy: https://www.cloudflare.com/ips/

            // Remove the default loopback-only network allowlist so the next lines fully define trust.
            options.KnownIPNetworks.Clear();
            // Remove the default loopback proxy IPs so they do not mix with a custom proxy CIDR.
            options.KnownProxies.Clear();

            // Docker user-defined bridges usually sit in 172.16.0.0/12, `proxy`.
            foreach (var candidateString in configurationModel.TrustedNetworks)
            {
                if (!candidateString.TryParseCidrAddressToNetwork(out var network))
                {
                    continue;
                }

                options.KnownIPNetworks.Add(network);
            }
        });
    }

    public static IApplicationBuilder ConsiderUsingForwardedHeaders(this WebApplication app)
    {
        if (GetConfigurationModel(app.Configuration).RespectHeaders)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private sealed class ConfigurationModel
    {
        public required bool RespectHeaders { get; init; }
        public required List<string> TrustedNetworks { get; init; }
    }
}