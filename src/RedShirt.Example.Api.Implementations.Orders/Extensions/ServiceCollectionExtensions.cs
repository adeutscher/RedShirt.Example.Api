using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Implementations.Orders.Models.Generated;

namespace RedShirt.Example.Api.Implementations.Orders.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrders(this IServiceCollection services)
    {
        return services.AddGeneratedOrder();
    }
}
