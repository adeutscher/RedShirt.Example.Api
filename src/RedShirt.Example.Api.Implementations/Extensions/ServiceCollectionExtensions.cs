using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.Extensions;
using RedShirt.Example.Api.Core.Extensions;
using RedShirt.Example.Api.Core.Repositories;
using RedShirt.Example.Api.Implementations.Repositories;

namespace RedShirt.Example.Api.Implementations.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApiImplementations(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAwsServiceWithLocalSupport<IAmazonDynamoDB>()
            .AddSingleton<IDynamoDBContext, DynamoDBContext>()
            .AddSingleton<IExampleItemRepository, DynamoExampleItemRepository>()
            .Configure<DynamoExampleItemRepository.ConfigurationModel>(configuration.GetSection("Storage:ExampleItem"))
            .ConfigureApiCore(configuration);
    }
}