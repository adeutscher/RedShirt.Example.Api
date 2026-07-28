using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;

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
            .AddCqrsHandlers(typeof(ServiceCollectionExtensions).Assembly);
    }
}