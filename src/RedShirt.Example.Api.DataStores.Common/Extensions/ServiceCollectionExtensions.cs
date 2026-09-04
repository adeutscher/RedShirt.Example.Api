using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.DataStores.Common.Services;

namespace RedShirt.Example.Api.DataStores.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonDatabase(this IServiceCollection services)
    {
        return services
            .AddSingleton<IConnectionStringSource, ConnectionStringSource>();
    }
}