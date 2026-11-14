using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;
using RedShirt.Example.Api.Core.UseCases.Upload.Services;

namespace RedShirt.Example.Api.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApiCore(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddSingleton<ICacheBasedIdempotencyService, CacheBasedIdempotencyService>()
            .AddSingleton<ICacheBasedIdempotencyWrapperService, CacheBasedIdempotencyWrapperService>()
            .Configure<CacheBasedIdempotencyService.ConfigurationModel>(configuration.GetSection("Core:Idempotency"))
            .AddSingleton<ICoreRequestValidator, CoreRequestValidator>()
            // Background jobs run inside the API reach the job table and the upload system through CQRS handlers
            .AddTransient<IBackgroundJobSource, CqrsBackgroundJobSource>()
            .AddTransient<IUploadValidationService, CqrsUploadValidationService>()
            .AddTransient<IUploadStorageService, CqrsUploadStorageService>()
            .AddCqrsHandlers(typeof(ServiceCollectionExtensions).Assembly);
    }
}