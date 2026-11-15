using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Cleanup.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the hosted service that deletes old finished jobs from the BackgroundJob table. It does nothing unless
    ///     <c>BACKGROUND_JOBS__CLEANUP_ENABLED=true</c>, and then it requires <c>BACKGROUND_JOBS__CLEANUP_RETENTION_DAYS</c>
    ///     (checked at startup). The host must also register the API Core CQRS handlers.
    /// </summary>
    public static IServiceCollection AddBackgroundJobCleanup(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<BackgroundJobCleanupOptions>()
            .Bind(configuration.GetSection(BackgroundJobCleanupOptions.ConfigurationSectionName))
            .Validate(options => !options.CleanupEnabled || options.CleanupRetentionDays is >= 1,
                "BACKGROUND_JOBS__CLEANUP_RETENTION_DAYS must be set to at least 1 when background job cleanup is enabled.")
            .ValidateOnStart();
        return services.AddHostedService<BackgroundJobCleanupHostedService>();
    }
}
