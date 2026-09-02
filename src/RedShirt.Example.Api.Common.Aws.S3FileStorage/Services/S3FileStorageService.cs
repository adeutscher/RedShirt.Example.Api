using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.FileStorage.Services;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;

internal sealed class S3FileStorageService(IAmazonS3 s3) : IFileStorageService, IDisposable
{
    private readonly TransferUtility _transferUtility = new(s3);

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

    public void Dispose()
    {
        _transferUtility.Dispose();
    }

    public async Task<FileStorageUploadResult> UploadAsync(string bucketName, string objectKey, Stream content,
        long? contentLength = null, CancellationToken cancellationToken = default)
    {
        /*
         * Upload flow:
         *   Content ──ReadAsync──▶ HashingStream ──ReadAsync──▶ TransferUtility.UploadAsync
         *
         * Kestrel's Request.Body forbids synchronous reads by default (AllowSynchronousIO = false).
         * PutObjectAsync sync-reads InputStream when marshalling the body; see
         * https://github.com/aws/aws-sdk-net/issues/1452 and https://github.com/aws/aws-sdk-net/issues/1534.
         *
         * TransferUtility.UploadAsync reads non-seekable streams via ReadAsync and buffers at most one
         * multipart part (default 5 MB) at a time instead of loading the entire object into memory.
         * HashingStream forwards any synchronous SDK reads to ReadAsync on the upstream stream.
         */
        await using var hashingStream = new HashingStream(content);
        var resolvedContentLength = ResolveContentLength(hashingStream, contentLength);

        var request = new TransferUtilityUploadRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = hashingStream,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Headers =
            {
                ContentLength = resolvedContentLength
            }
        };

        await _transferUtility.UploadAsync(request, cancellationToken).ConfigureAwait(false);

        if (hashingStream.BytesRead != resolvedContentLength)
        {
            throw new InvalidOperationException(
                $"Upload stream ended after {hashingStream.BytesRead} bytes; Content-Length declared {resolvedContentLength}.");
        }

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
        string? downloadFileName = null, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validity)
        };

        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            var escapedFileName = downloadFileName.Replace("\"", "\\\"", StringComparison.Ordinal);
            request.ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{escapedFileName}\""
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await s3.GetPreSignedURLAsync(request);
    }
}