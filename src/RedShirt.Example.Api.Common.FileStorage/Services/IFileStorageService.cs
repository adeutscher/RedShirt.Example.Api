namespace RedShirt.Example.Api.Common.FileStorage.Services;

/// <summary>
///     Platform-agnostic blob storage abstraction. The original design target was an S3-shaped API, but
///     implementations may wrap Azure Blob Storage, Google Cloud Storage, MinIO, or similar services.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    ///     Deletes the object at <paramref name="objectKey" /> from <paramref name="bucketName" />.
    /// </summary>
    Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a time-limited URL that allows direct download of the object without streaming through the API.
    ///     Presigned URLs are supported by AWS S3, Azure Blob Storage, Google Cloud Storage, MinIO, and others.
    /// </summary>
    /// <param name="downloadFileName">
    ///     Optional file name suggested to the client via <c>Content-Disposition</c> on the presigned response.
    /// </param>
    Task<string> GetPresignedDownloadUrlAsync(string bucketName, string objectKey, TimeSpan validity,
        string? downloadFileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams <paramref name="content" /> into <paramref name="bucketName" /> at
    ///     <paramref name="objectKey" /> without buffering the entire payload in memory.
    /// </summary>
    /// <param name="contentLength">
    ///     Known byte length of <paramref name="content" />. Required when the stream is not seekable
    ///     (for example HTTP request bodies).
    /// </param>
    Task<FileStorageUploadResult> UploadAsync(string bucketName, string objectKey, Stream content,
        long? contentLength = null, CancellationToken cancellationToken = default);
}

public sealed class FileStorageUploadResult
{
    public required string Sha256Checksum { get; init; }
}