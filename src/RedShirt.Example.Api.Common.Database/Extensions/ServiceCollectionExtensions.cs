using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.Services;

namespace RedShirt.Example.Api.Common.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonDatabase(this IServiceCollection services)
    {
        return services
            .AddSingleton<IConnectionStringSource, ConnectionStringSource>();
    }
}