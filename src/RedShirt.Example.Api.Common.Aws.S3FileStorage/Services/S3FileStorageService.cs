using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.FileStorage.Services;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;

internal sealed class S3FileStorageService(IAmazonS3 s3) : IFileStorageService
{
    public async Task<FileStorageUploadResult> UploadAsync(string bucketName, string objectKey, Stream content,
        CancellationToken cancellationToken = default)
    {
        await using var hashingStream = new HashingStream(content);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = hashingStream,
            AutoCloseStream = false
        };

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
