using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.DapperMySql.Extensions;
using RedShirt.Example.Api.Implementations.Products.Models;
using RedShirt.Example.Api.Implementations.Products.Repositories;
using RedShirt.Example.Api.Implementations.Products.Services;

namespace RedShirt.Example.Api.Implementations.Products.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProducts(this IServiceCollection services)
    {
        return services
            .AddSingleton<IProductService, ProductService>()
            .AddSingleton<IProductRepository, MariaDbProductRepository>()
            .AddGenericMysqlDtoHandler<ProductDto, Guid>();
    }
}
