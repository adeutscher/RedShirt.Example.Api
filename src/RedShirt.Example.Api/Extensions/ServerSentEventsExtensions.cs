using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.ServerSentEvents;

namespace RedShirt.Example.Api.Extensions;

internal static class ServerSentEventsExtensions
{
    internal const string UnlimitedConnectionsWarning =
        "ServerSentEvents:MaxConnectionsPerUser is unset, zero, or negative, so one user can open any number of event " +
        "streams. Each stream holds an HTTP connection and its own MQTT broker connection. Set " +
        "SERVER_SENT_EVENTS__MAX_CONNECTIONS_PER_USER to a positive number. See docs/patterns/server-sent-events.md.";

    internal static IServiceCollection AddServerSentEventConnectionLimits(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServerSentEventsOptions>(
            configuration.GetSection(ServerSentEventsOptions.ConfigurationSectionName));
        services.AddSingleton<IServerSentEventConnectionLimiter, ServerSentEventConnectionLimiter>();
        return services;
    }

    /// <summary>
    ///     Logs a warning at startup when event streams per user are unlimited. This applies in every environment,
    ///     Development included, because the local stack sets a limit.
    /// </summary>
    internal static WebApplication WarnIfServerSentEventConnectionsUnlimited(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ServerSentEventsOptions>>().Value;
        if (!options.IsLimited)
        {
            app.Logger.LogWarning(UnlimitedConnectionsWarning);
        }

        return app;
    }
}
