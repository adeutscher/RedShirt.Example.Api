using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.Services.Topics.ExampleItem;

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
            .AddSingleton<IExampleItemService, ExampleItemService>();
    }
}