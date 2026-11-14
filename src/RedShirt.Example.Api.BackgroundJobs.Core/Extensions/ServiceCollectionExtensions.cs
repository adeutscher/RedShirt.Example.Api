using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Services shared by every part of the background job system. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddBackgroundJobsCore(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IBackgroundJobWakeSignal, BackgroundJobWakeSignal>();
        return services
            .Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.ConfigurationSectionName));
    }
}
