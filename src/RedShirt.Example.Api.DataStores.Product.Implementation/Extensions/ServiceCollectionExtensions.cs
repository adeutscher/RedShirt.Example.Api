using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Extensions;
using RedShirt.Example.Api.DataStores.Product.Core.Services;
using RedShirt.Example.Api.DataStores.Product.Implementation.Entities;
using RedShirt.Example.Api.DataStores.Product.Implementation.Repositories;
using RedShirt.Example.Api.DataStores.Product.Implementation.Services;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProducts(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddSingleton<IProductService, ProductService>()
            .AddSingleton<IProductRepository, MariaDbProductRepository>()
            .AddGenericMysqlDtoHandler<ProductEntity, Guid>();
    }
}