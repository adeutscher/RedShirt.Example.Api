using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.BackgroundJobs.Core.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Submission.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Table.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Table.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Submits background jobs to the BackgroundJob table, for the in-API runner. Requires the BackgroundJob data
    ///     store (<c>AddBackgroundJobs</c>).
    /// </summary>
    public static IServiceCollection AddTableBackgroundJobSubmitter(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddBackgroundJobsCore(configuration)
            .AddSingleton<IBackgroundJobSubmitter, TableBackgroundJobSubmitter>();
    }
}
