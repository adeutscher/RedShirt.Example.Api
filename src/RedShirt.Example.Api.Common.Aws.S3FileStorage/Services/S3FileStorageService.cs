using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.FileStorage.Services;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;

internal sealed class S3FileStorageService(IAmazonS3 s3) : IFileStorageService
{
    private static long ResolveContentLength(Stream content, long? contentLength)
    {
        if (contentLength is >= 0)
        {
            return contentLength.Value;
        }

        if (content.CanSeek)
        {
            return content.Length;
        }

        throw new ArgumentException(
            "Content length must be provided when the stream is not seekable.",
            nameof(contentLength));
    }

    public async Task<FileStorageUploadResult> UploadAsync(string bucketName, string objectKey, Stream content,
        long? contentLength = null, CancellationToken cancellationToken = default)
    {
        await using var hashingStream = new HashingStream(content);
        var resolvedContentLength = ResolveContentLength(hashingStream, contentLength);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = hashingStream,
            AutoCloseStream = false
        };
        request.Headers.ContentLength = resolvedContentLength;

        await s3.PutObjectAsync(request, cancellationToken);

        return new FileStorageUploadResult
        {
            Sha256Checksum = hashingStream.GetSha256Hex()
        };
    }

    public Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        return s3.DeleteObjectAsync(bucketName, objectKey, cancellationToken);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(string bucketName, string objectKey, TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validity)
        };

        cancellationToken.ThrowIfCancellationRequested();
        return await s3.GetPreSignedURLAsync(request);
    }
}