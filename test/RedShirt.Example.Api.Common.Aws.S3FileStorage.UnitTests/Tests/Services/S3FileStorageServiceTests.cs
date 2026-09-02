using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.UnitTests.Support;
using System.Security.Cryptography;
using System.Text;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.UnitTests.Tests.Services;

public class S3FileStorageServiceTests
{
    private static string Sha256Hex(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static (Mock<IAmazonS3> S3, List<byte> CapturedBody) CreateCapturingS3Mock(long expectedObjectLength)
    {
        var capturedBody = new List<byte>();
        const string uploadId = "test-multipart-upload-id";
        var s3 = new Mock<IAmazonS3>(MockBehavior.Strict);

        s3.SetupGet(x => x.Config).Returns(new AmazonS3Config
        {
            ServiceURL = "http://localhost:4566",
            ForcePathStyle = true
        });

        // TransferUtility treats non-seekable streams as multipart uploads.
        s3.Setup(x => x.InitiateMultipartUploadAsync(
                It.IsAny<InitiateMultipartUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateMultipartUploadResponse {UploadId = uploadId});

        s3.Setup(x => x.UploadPartAsync(It.IsAny<UploadPartRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadPartRequest, CancellationToken>((request, _) =>
            {
                if (request.InputStream is null)
                {
                    return;
                }

                var remaining = expectedObjectLength - capturedBody.Count;
                if (remaining <= 0)
                {
                    return;
                }

                var buffer = new byte[(int) Math.Min(remaining, int.MaxValue)];
                var read = 0;
                while (read < buffer.Length)
                {
                    var bytesRead = request.InputStream.Read(buffer, read, buffer.Length - read);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    read += bytesRead;
                }

                capturedBody.AddRange(buffer.AsSpan(0, read).ToArray());
            })
            .ReturnsAsync((UploadPartRequest request, CancellationToken _) => new UploadPartResponse
            {
                ETag = "\"test-etag\"",
                PartNumber = request.PartNumber
            });

        s3.Setup(x => x.CompleteMultipartUploadAsync(
                It.IsAny<CompleteMultipartUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteMultipartUploadResponse());

        s3.Setup(x => x.AbortMultipartUploadAsync(
                It.IsAny<AbortMultipartUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbortMultipartUploadResponse());

        return (s3, capturedBody);
    }

    [Fact]
    public async Task UploadAsync_AsyncOnlySourceStream_DeliversFullPayloadToS3()
    {
        var payload = Encoding.UTF8.GetBytes("This upload contains a potato.");
        await using var source = new AsyncOnlyReadStream(payload, 5);
        var (s3, capturedBody) = CreateCapturingS3Mock(payload.LongLength);

        using var service = new S3FileStorageService(s3.Object);
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
            x => x.InitiateMultipartUploadAsync(
                It.Is<InitiateMultipartUploadRequest>(r =>
                    r.BucketName == "unverified-uploads" && r.Key == "upload-id/user-id"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        s3.Verify(
            x => x.UploadPartAsync(It.IsAny<UploadPartRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        s3.Verify(
            x => x.CompleteMultipartUploadAsync(
                It.IsAny<CompleteMultipartUploadRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_LargeAsyncOnlyPayload_StillDeliversAllBytes()
    {
        var payload = Encoding.UTF8.GetBytes(new string('p', 12_000) + "otato");
        await using var source = new AsyncOnlyReadStream(payload, 256);
        var (s3, capturedBody) = CreateCapturingS3Mock(payload.LongLength);

        using var service = new S3FileStorageService(s3.Object);
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
        var (s3, _) = CreateCapturingS3Mock(payload.LongLength);

        using var service = new S3FileStorageService(s3.Object);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync("bucket", "key", source, null,
                TestContext.Current.CancellationToken));

        Assert.Contains("Content length must be provided", exception.Message, StringComparison.Ordinal);
        s3.Verify(
            x => x.InitiateMultipartUploadAsync(
                It.IsAny<InitiateMultipartUploadRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadAsync_ThrowsWhenStreamEndsBeforeDeclaredContentLength()
    {
        var payload = Encoding.UTF8.GetBytes("partial");
        await using var source = new AsyncOnlyReadStream(payload);
        var (s3, _) = CreateCapturingS3Mock(payload.LongLength + 10);

        using var service = new S3FileStorageService(s3.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync("bucket", "key", source, payload.LongLength + 10,
                TestContext.Current.CancellationToken));

        Assert.Contains("Content-Length declared", exception.Message, StringComparison.Ordinal);
    }
}