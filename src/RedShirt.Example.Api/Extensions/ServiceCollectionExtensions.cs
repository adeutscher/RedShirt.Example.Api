using RedShirt.Example.Api.Common.Shared.Implementation.InMemory.Extensions;
using RedShirt.Example.Api.ExceptionHandlers;
using RedShirt.Example.Api.Implementations.Extensions;

namespace RedShirt.Example.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureApiServices(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        return serviceCollection
            .AddProblemDetails()
            .AddExceptionHandler<ApiExceptionHandler>()
            .AddInMemorySharedImplementations()
            .ConfigureApiImplementations(configuration);
    }
}