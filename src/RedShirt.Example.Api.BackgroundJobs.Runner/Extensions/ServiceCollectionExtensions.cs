using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.BackgroundJobs.Core.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Runner.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Runner.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the hosted service that runs background jobs inside this process. The host must also register an
    ///     <see cref="IBackgroundJobSource" /> and an <see cref="IJobLogicRunner" />.
    ///     The runner can be switched off with <c>BACKGROUND_JOBS__RUNNER_ENABLED=false</c>.
    /// </summary>
    public static IServiceCollection AddBackgroundJobRunner(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddBackgroundJobsCore(configuration)
            .AddHostedService<BackgroundJobRunnerHostedService>();
    }
}
