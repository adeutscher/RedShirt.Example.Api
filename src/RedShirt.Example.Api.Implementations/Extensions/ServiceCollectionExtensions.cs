using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Core.Extensions;
using RedShirt.Example.Api.Core.Repositories;
using RedShirt.Example.Api.Implementations.Repositories;

namespace RedShirt.Example.Api.Implementations.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsServiceWithLocalSupport<TService>(this IServiceCollection services)
        where TService : IAmazonService
    {
        var url = Environment.GetEnvironmentVariable("AWS_SERVICE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            return services
                .AddAWSService<TService>();
        }

        // Note: S3 needs a special carve-out for AmazonS3Config.ForcePathStyle that is not needed here.

        Console.WriteLine($"Using AWS service URL: {url}");

        return services.AddAWSService<TService>(new AWSOptions
        {
            DefaultClientConfig =
            {
                ServiceURL = url
            }
        });
    }

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