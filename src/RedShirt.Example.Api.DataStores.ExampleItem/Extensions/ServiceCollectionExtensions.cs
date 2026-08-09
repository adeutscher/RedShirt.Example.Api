using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.Extensions;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;
using RedShirt.Example.Api.DataStores.ExampleItem.Repositories;

namespace RedShirt.Example.Api.DataStores.ExampleItem.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExampleItem(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAwsServiceWithLocalSupport<IAmazonDynamoDB>()
            .AddSingleton<IDynamoDBContext, DynamoDBContext>()
            .AddSingleton<IExampleItemRepository, DynamoExampleItemRepository>()
            .Configure<DynamoExampleItemRepository.ConfigurationModel>(configuration.GetSection("Storage:ExampleItem"));
    }
}