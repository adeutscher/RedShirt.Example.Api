using RedShirt.Example.Api.Common.Aws.S3FileStorage.IntegrationTests.Support;
using System.Security.Cryptography;
using System.Text;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.IntegrationTests.Tests.Services;

public class S3FileStorageServiceIntegrationTests
{
    private static string Sha256Hex(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    [Fact]
    public async Task UploadAsync_AsyncOnlyStream_PersistsExactBytesInMinistack()
    {
        var environment = await MinistackTestEnvironment.TryCreateAsync(TestContext.Current.CancellationToken);
        MinistackTestEnvironment.SkipUnlessAvailable(environment);
        var (s3, service) = environment!.Value;

        var payload = Encoding.UTF8.GetBytes("Integration upload contains a potato.");
        await using var source = new AsyncOnlyReadStream(payload, 7);
        var objectKey = $"integration-tests/{Guid.NewGuid():N}";

        try
        {
            var result = await service.UploadAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                source,
                payload.LongLength,
                TestContext.Current.CancellationToken);

            Assert.Equal(0, source.SyncReadAttempts);
            Assert.Equal(Sha256Hex(payload), result.Sha256Checksum);

            using var getResponse = await s3.GetObjectAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                TestContext.Current.CancellationToken);
            Assert.Equal(payload.LongLength, getResponse.ContentLength);

            using var actualStream = new MemoryStream();
            await getResponse.ResponseStream.CopyToAsync(
                actualStream,
                TestContext.Current.CancellationToken);
            Assert.Equal(payload, actualStream.ToArray());
        }
        finally
        {
            await s3.DeleteObjectAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                TestContext.Current.CancellationToken);
            s3.Dispose();
        }
    }

    [Fact]
    public async Task UploadAsync_LargeAsyncOnlyStream_PersistsExactBytesInMinistack()
    {
        var environment = await MinistackTestEnvironment.TryCreateAsync(TestContext.Current.CancellationToken);
        MinistackTestEnvironment.SkipUnlessAvailable(environment);
        var (s3, service) = environment!.Value;

        var payload = Encoding.UTF8.GetBytes(new string('p', 12_000) + "otato");
        await using var source = new AsyncOnlyReadStream(payload, 512);
        var objectKey = $"integration-tests/{Guid.NewGuid():N}";

        try
        {
            var result = await service.UploadAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                source,
                payload.LongLength,
                TestContext.Current.CancellationToken);

            Assert.Equal(Sha256Hex(payload), result.Sha256Checksum);

            using var getResponse = await s3.GetObjectAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                TestContext.Current.CancellationToken);
            Assert.Equal(payload.LongLength, getResponse.ContentLength);

            using var actualStream = new MemoryStream();
            await getResponse.ResponseStream.CopyToAsync(
                actualStream,
                TestContext.Current.CancellationToken);
            Assert.Equal(payload, actualStream.ToArray());
        }
        finally
        {
            await s3.DeleteObjectAsync(
                MinistackTestEnvironment.IntegrationBucketName,
                objectKey,
                TestContext.Current.CancellationToken);
            s3.Dispose();
        }
    }
}