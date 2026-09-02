using Amazon.S3;
using Amazon.S3.Model;
using RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;
using RedShirt.Example.Api.Common.FileStorage.Services;
using System.IO.Pipelines;

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
         * We pump the source into a Pipe using only ReadAsync. PutObjectAsync still sync-reads
         * InputStream when marshalling the body, but we avoid TransferUtility multipart mode,
         * which pads non-seekable pipe streams to the minimum part size and produces oversized
         * (often zero-filled) S3 objects. The SDK sync-reads from pipe.Reader.AsStream(), which
         * is an in-memory buffer — not the HTTP request stream.
         * HashingStream rejects sync Read on the upstream stream so nothing can bypass this path.
         */
        var pipe = new Pipe();
        await using var hashingStream = new HashingStream(content);
        var resolvedContentLength = ResolveContentLength(hashingStream, contentLength);
        var pumpTask = AsyncStreamPump.PumpAsync(hashingStream, pipe.Writer, cancellationToken);

        // Finish pumping before the SDK sync-reads the pipe. Concurrent upload + sync Read from
        // PipeReader.AsStream() can observe trailing buffer bytes as zeros, producing oversized
        // S3 objects even when the SHA-256 over the async source path is correct.
        await pumpTask.ConfigureAwait(false);
        await UploadPipeToS3Async(
            pipe.Reader, bucketName, objectKey, resolvedContentLength, cancellationToken).ConfigureAwait(false);

        return new FileStorageUploadResult
        {
            Sha256Checksum = hashingStream.GetSha256Hex()
        };
    }

    private async Task UploadPipeToS3Async(
        PipeReader reader,
        string bucketName,
        string objectKey,
        long contentLength,
        CancellationToken cancellationToken)
    {
        await using var uploadStream = reader.AsStream();

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = uploadStream,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Headers =
            {
                ContentLength = contentLength
            }
        };

        await s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
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