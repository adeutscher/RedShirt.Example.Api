using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the job logic runner and the job handlers. The host must also register the services that the handlers
    ///     call back into (such as <see cref="IUploadValidationService" />).
    /// </summary>
    public static IServiceCollection AddBackgroundJobLogic(this IServiceCollection services)
    {
        return services
            .AddTransient<IJobLogicRunner, BackgroundJobLogicRunner>()
            .AddTransient<IBackgroundJobHandler, ValidateUploadJobHandler>();
    }
}
