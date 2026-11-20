using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Configuration;

namespace RedShirt.Example.Api.Extensions;

internal static class ExceptionHandlingExtensions
{
    internal const string ExposingWarning =
        "ExceptionHandling:ExposeUpstreamExceptionMessages is on: 502 responses include upstream exception " +
        "messages. This is for local development only.";

    internal const string IgnoredWarning =
        "ExceptionHandling:ExposeUpstreamExceptionMessages is set but ignored: upstream exception messages are only " +
        "exposed in the Development environment.";

    internal static IServiceCollection AddApiExceptionHandlingOptions(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.Configure<ExceptionHandlingOptions>(
            configuration.GetSection(ExceptionHandlingOptions.ConfigurationSectionName));
    }

    /// <summary>
    ///     Logs at startup when upstream exception messages are exposed, or when the setting is on but ignored
    ///     because the environment is not Development.
    /// </summary>
    internal static WebApplication WarnAboutExceptionMessageExposure(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ExceptionHandlingOptions>>().Value;
        if (!options.ExposeUpstreamExceptionMessages)
        {
            return app;
        }

        app.Logger.LogWarning(options.EffectiveExposeUpstreamExceptionMessages(app.Environment)
            ? ExposingWarning
            : IgnoredWarning);
        return app;
    }
}