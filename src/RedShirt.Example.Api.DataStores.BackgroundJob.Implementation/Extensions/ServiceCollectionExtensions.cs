using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Configuration;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Factories;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Services;
using RedShirt.Example.Api.DataStores.Common.Extensions;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the BackgroundJob table's storage service (Entity Framework Core, MariaDB / MySQL).
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddCommonDatabase()
            .Configure<BackgroundJobStoreOptions>(
                configuration.GetSection(BackgroundJobStoreOptions.ConfigurationSectionName))
            .AddSingleton<IBackgroundJobDbContextFactory, BackgroundJobDbContextFactory>()
            .AddSingleton<IBackgroundJobService, BackgroundJobService>();
    }
}