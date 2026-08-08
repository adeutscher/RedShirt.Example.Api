using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.DapperMySql.Factories;
using RedShirt.Example.Api.Common.Database.DapperMySql.Services;
using RedShirt.Example.Api.Common.Database.Extensions;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDapperMySql(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddCommonDatabase()
            .AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
    }

    public static IServiceCollection AddGenericDtoHandler<TDto, TKey>(this IServiceCollection services)
        where TDto : class
    {
        return services
            .AddSingleton<IGenericMySqlDtoStorage<TDto, TKey>, GenericMySqlDtoStorage<TDto, TKey>>();
    }
}