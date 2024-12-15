using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Services;

namespace RedShirt.Example.Api.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApiCore(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddSingleton<IExampleItemService, ExampleItemService>();
    }
}