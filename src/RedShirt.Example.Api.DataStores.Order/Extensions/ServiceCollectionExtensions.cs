using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.DataStores.Order.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrders(this IServiceCollection services)
    {
        return services.AddGeneratedOrder();
    }
}
