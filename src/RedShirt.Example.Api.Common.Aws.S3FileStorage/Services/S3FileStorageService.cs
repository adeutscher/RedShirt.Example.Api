using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.FileStorage.Services;
using System.IO.Pipelines;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Services;

internal sealed class S3FileStorageService(IAmazonS3 s3) : IFileStorageService, IDisposable
{
    /// <summary>
    ///     Transfer facilitator.
    ///     Using TransferUtility instead of a raw PutObjectRequest through the S3 client because of difficulties in local
    ///     testing with the measures we had to take to avoid non-async reads.
    /// </summary>
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
         *   Content ──ReadAsync──▶ HashingStream ──ReadAsync──▶ AsyncStreamPump ──▶ Pipe
         *                                                                                 │
         *                                                            sync Read ◀── TransferUtility.UploadAsync
         *
         * TransferUtility.UploadAsync is async at the HTTP layer, but the AWS SDK for .NET still reads
         * InputStream synchronously (Stream.Read) when marshalling the body. The same is true of
         * PutObjectAsync. See https://github.com/aws/aws-sdk-net/issues/1452 and
         * https://github.com/aws/aws-sdk-net/issues/1534.
         *
         * Though this method is written to be agnostic, it was originally written for streaming upload requests for an ASP.NET Kestrel API.
         * Kestrel's Request.Body forbids synchronous reads by default (AllowSynchronousIO = false).
         * Passing Request.Body directly to the SDK therefore fails with a message like:
         *   "Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true."
         *
         * We pump the source into a Pipe using only ReadAsync. The SDK sync-reads from
         * pipe.Reader.AsStream(), which is an in-memory buffer — not the HTTP request stream.
         * HashingStream rejects sync Read on the upstream stream so nothing can bypass this path.
         */
        var pipe = new Pipe();
        await using var hashingStream = new HashingStream(content);
        var resolvedContentLength = ResolveContentLength(hashingStream, contentLength);
        var pumpTask = AsyncStreamPump.PumpAsync(hashingStream, pipe.Writer, cancellationToken);

        try
        {
            await using var uploadStream = pipe.Reader.AsStream();

            var request = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = uploadStream,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                Headers =
                {
                    ContentLength = resolvedContentLength
                }
            };

            await _transferUtility.UploadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Ensure every byte was pumped (and hashed) even if UploadAsync fails mid-upload.
            await pumpTask.ConfigureAwait(false);
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