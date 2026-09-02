using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;
using RedShirt.Example.Api.Common.FileStorage.Services;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3FileStorage(this IServiceCollection services)
    {
        return services
            .AddAwsS3WithLocalSupport()
            .AddSingleton<IFileStorageService, S3FileStorageService>();
    }

    private static IServiceCollection AddAwsS3WithLocalSupport(this IServiceCollection services)
    {
        var url = Environment.GetEnvironmentVariable("AWS_SERVICE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            return services.AddAWSService<IAmazonS3>();
        }

        Console.WriteLine($"Using AWS service URL: {url}");
        
        // S3 needs a special config carve-out
        var s3Config = new AmazonS3Config
        {
            ServiceURL = url,
            // Force path style, as opposed to a DNS-based name
            ForcePathStyle = true
        };

        return services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(s3Config));
    }
}
