using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Factories;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Services;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Services.Resilience;
using RedShirt.Example.Api.DataStores.Common.Extensions;

namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDapperMySql(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddCommonDatabase()
            .AddSingleton<IMySqlExceptionArbiterService, MySqlExceptionArbiterService>()
            .AddSingleton<IMySqlRetryWrapperService, MySqlRetryWrapperService>()
            .AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
    }

    public static IServiceCollection AddGenericMysqlDtoHandler<TDto, TKey>(this IServiceCollection services)
        where TDto : class
    {
        return services
            .AddSingleton<IGenericMySqlDtoStorage<TDto, TKey>, GenericMySqlDtoStorage<TDto, TKey>>();
    }
}