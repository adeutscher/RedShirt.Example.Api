using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.IntegrationTests.Support;

internal static class MinistackTestEnvironment
{
    public const string DefaultServiceUrl = "http://localhost:4566";
    public const string IntegrationBucketName = "unverified-uploads";

    public static string ServiceUrl =>
        Environment.GetEnvironmentVariable("AWS_SERVICE_URL") ?? DefaultServiceUrl;

    public static AmazonS3Config CreateS3Config()
    {
        return new AmazonS3Config
        {
            ServiceURL = ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };
    }

    public static AmazonS3Client CreateClient()
    {
        var credentials = new BasicAWSCredentials(
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "foo",
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "bar");

        return new AmazonS3Client(credentials, CreateS3Config());
    }

    public static async Task<(AmazonS3Client Client, S3FileStorageService Service)?> TryCreateAsync(
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        try
        {
            await client.GetBucketLocationAsync(IntegrationBucketName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            client.Dispose();
            return null;
        }

        return (client, new S3FileStorageService(client));
    }

    public static void SkipUnlessAvailable((AmazonS3Client Client, S3FileStorageService Service)? environment)
    {
        if (environment is null)
        {
            Assert.Skip(
                $"Ministack S3 bucket '{IntegrationBucketName}' is not reachable at {ServiceUrl}. " +
                "Start ministack and run test/local/make-local-aws-resources.sh.");
        }
    }
}
