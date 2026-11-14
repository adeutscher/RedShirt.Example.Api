using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Core.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Factories;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Services;
using RedShirt.Example.Api.DataStores.Common.Extensions;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the BackgroundJob table: its storage service, and the submitter that writes to it.
    /// </summary>
    public static IServiceCollection AddBackgroundJobTable(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddBackgroundJobsCore(configuration)
            .AddCommonDatabase()
            .AddSingleton<IBackgroundJobDbContextFactory, BackgroundJobDbContextFactory>()
            .AddSingleton<IBackgroundJobService, BackgroundJobService>()
            .AddSingleton<IBackgroundJobSubmitter, TableBackgroundJobSubmitter>();
    }
}
