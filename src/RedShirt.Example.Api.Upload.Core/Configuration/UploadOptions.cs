namespace RedShirt.Example.Api.Upload.Core.Configuration;

public sealed class UploadOptions
{
    public const string ConfigurationSectionName = "Uploads";

    /// <summary>
    ///     When set, limits the maximum request body size for upload endpoints (Kestrel).
    ///     Environment variable: <c>UPLOADS__MAX_UPLOAD_SIZE_BYTES</c>.
    /// </summary>
    public long? MaxUploadSizeBytes { get; init; }

    /// <summary>
    ///     Bucket for files that have not yet been validated. Environment variable:
    ///     <c>UPLOADS__BUCKET_UNVERIFIED_ITEMS</c>.
    /// </summary>
    public string BucketUnverifiedItems { get; init; } = "unverified-uploads";

    /// <summary>
    ///     Bucket for validated files that have been moved to trusted storage. Environment variable:
    ///     <c>UPLOADS__BUCKET_VERIFIED_ITEMS</c>.
    /// </summary>
    public string BucketVerifiedItems { get; init; } = "verified-uploads";

    /// <summary>
    ///     Lifetime of presigned download URLs returned by GET <c>/uploads/{id}/download-link</c>.
    /// </summary>
    public int PresignedUrlLifetimeMinutes { get; init; } = 15;
}
