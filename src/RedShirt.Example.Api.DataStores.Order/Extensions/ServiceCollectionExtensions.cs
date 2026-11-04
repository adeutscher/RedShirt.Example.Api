using Microsoft.Extensions.DependencyInjection;

namespace RedShirt.Example.Api.DataStores.Order.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrders(this IServiceCollection services)
    {
        return services.AddGeneratedOrder();
    }
}