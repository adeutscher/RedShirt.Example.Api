using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.DapperMySql.Extensions;
using RedShirt.Example.Api.Core.UseCases.Product.Services;
using RedShirt.Example.Api.Implementations.Products.Models;
using RedShirt.Example.Api.Implementations.Products.Repositories;

namespace RedShirt.Example.Api.Implementations.Products.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProducts(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddDapperMySql(configuration)
            .AddSingleton<IProductRepository, MariaDbProductRepository>()
            .AddGenericMysqlDtoHandler<ProductDto, Guid>();
    }
}
