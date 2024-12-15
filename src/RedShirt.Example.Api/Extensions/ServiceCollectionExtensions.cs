using RedShirt.Example.Api.Implementations.Extensions;

namespace RedShirt.Example.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureApiServices(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        return serviceCollection
            .ConfigureApiImplementations(configuration);
    }
}