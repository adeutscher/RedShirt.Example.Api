using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.BackgroundJobs.Core.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Implementation.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Submits background jobs: records them in the BackgroundJob table and hands them to the in-API runner (or, once
    ///     the enqueue point in <see cref="BackgroundJobSubmitter" /> is filled in, to an applied JobWorker's queue).
    ///     Requires the BackgroundJob data store (<c>AddBackgroundJobs</c>).
    /// </summary>
    public static IServiceCollection AddBackgroundJobSubmitter(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddBackgroundJobsCore(configuration)
            .AddSingleton<IBackgroundJobSubmitter, BackgroundJobSubmitter>();
    }
}
