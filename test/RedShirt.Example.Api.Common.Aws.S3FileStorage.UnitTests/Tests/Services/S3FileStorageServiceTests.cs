using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.UnitTests.Support;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.UnitTests.Tests.Services;

public class S3FileStorageServiceTests
{
    private static string Sha256Hex(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static (Mock<IAmazonS3> S3, List<byte> CapturedBody) CreateCapturingS3Mock()
    {
        var capturedBody = new List<byte>();
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);

        s3.SetupGet(x => x.Config).Returns(new AmazonS3Config
        {
            ServiceURL = "http://localhost:4566",
            ForcePathStyle = true
        });

        // PutObjectAsync sync-reads exactly ContentLength bytes from the pipe stream.
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) =>
            {
                if (request.InputStream is null)
                {
                    return;
                }

                var length = request.Headers.ContentLength;
                var buffer = new byte[length];
                var read = 0;
                while (read < length)
                {
                    var bytesRead = request.InputStream.Read(buffer, read, (int)(length - read));
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    read += bytesRead;
                }

                capturedBody.Clear();
                capturedBody.AddRange(buffer.AsSpan(0, read).ToArray());
            })
            .ReturnsAsync(new PutObjectResponse());

        return (s3, capturedBody);
    }

    [Fact]
    public async Task UploadAsync_AsyncOnlySourceStream_DeliversFullPayloadThroughPumpToS3()
    {
        var payload = Encoding.UTF8.GetBytes("This upload contains a potato.");
        await using var source = new AsyncOnlyReadStream(payload, chunkSize: 5);
        var (s3, capturedBody) = CreateCapturingS3Mock();

        var service = new S3FileStorageService(s3.Object);
        var result = await service.UploadAsync(
            "unverified-uploads",
            "upload-id/user-id",
            source,
            payload.LongLength,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, source.SyncReadAttempts);
        Assert.Equal(payload, capturedBody.ToArray());
        Assert.Equal(Sha256Hex(payload), result.Sha256Checksum);
        s3.Verify(
            x => x.PutObjectAsync(
                It.Is<PutObjectRequest>(r =>
                    r.BucketName == "unverified-uploads" &&
                    r.Key == "upload-id/user-id" &&
                    r.Headers.ContentLength == payload.LongLength),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_LargeAsyncOnlyPayload_StillDeliversAllBytes()
    {
        var payload = Encoding.UTF8.GetBytes(new string('p', 12_000) + "otato");
        await using var source = new AsyncOnlyReadStream(payload, chunkSize: 256);
        var (s3, capturedBody) = CreateCapturingS3Mock();

        var service = new S3FileStorageService(s3.Object);
        var result = await service.UploadAsync(
            "unverified-uploads",
            "large-object",
            source,
            payload.LongLength,
            TestContext.Current.CancellationToken);

        Assert.Equal(payload, capturedBody.ToArray());
        Assert.Equal(Sha256Hex(payload), result.Sha256Checksum);
    }

    [Fact]
    public async Task UploadAsync_ThrowsWhenContentLengthMissingForNonSeekableStream()
    {
        var payload = Encoding.UTF8.GetBytes("potato");
        await using var source = new AsyncOnlyReadStream(payload);
        var (s3, _) = CreateCapturingS3Mock();

        var service = new S3FileStorageService(s3.Object);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync("bucket", "key", source, contentLength: null,
                TestContext.Current.CancellationToken));

        Assert.Contains("Content length must be provided", exception.Message, StringComparison.Ordinal);
        s3.Verify(
            x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class AsyncStreamPumpTests
{
    [Fact]
    public async Task PumpAsync_CopiesAllBytesFromAsyncOnlyStream()
    {
        var payload = Encoding.UTF8.GetBytes("chunked potato payload");
        await using var source = new AsyncOnlyReadStream(payload, chunkSize: 3);
        var pipe = new Pipe();

        var pumpTask = AsyncStreamPump.PumpAsync(source, pipe.Writer, TestContext.Current.CancellationToken);
        await using var output = pipe.Reader.AsStream();
        using var actual = new MemoryStream();

        await pumpTask;
        await output.CopyToAsync(actual, TestContext.Current.CancellationToken);

        Assert.Equal(0, source.SyncReadAttempts);
        Assert.Equal(payload, actual.ToArray());
    }
}
