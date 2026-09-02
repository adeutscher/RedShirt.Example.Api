namespace RedShirt.Example.Api.Upload.Core.Models.Responses;

public static class UploadDetailsInternalModelExtensions
{
    // ReSharper disable once ConvertToExtensionBlock
    public static UploadDetailsModel ToPublicDetailsModel(this UploadDetailsInternalModel details)
    {
        return new UploadDetailsModel
        {
            Id = details.Id,
            DateCreatedUtc = details.DateCreatedUtc,
            UploadedByUserId = details.UploadedByUserId,
            UploadedByUsername = details.UploadedByUsername,
            FileName = details.FileName,
            DateCompletedUtc = details.DateCompletedUtc,
            Sha256Checksum = details.Sha256Checksum,
            DateValidatedUtc = details.DateValidatedUtc,
            DateRejectedUtc = details.DateRejectedUtc,
            DateStoredUtc = details.DateStoredUtc,
            DateDeletedUtc = details.DateDeletedUtc
        };
    }

    public static UploadInternalDetailsModel ToInternalDetailsModel(this UploadDetailsInternalModel details)
    {
        return new UploadInternalDetailsModel
        {
            UploaderIpAddress = details.UploaderIpAddress,
            StorageObjectKey = details.DateStoredUtc.HasValue
                ? details.VerifiedStorageObjectKey
                : details.StorageObjectKey
        };
    }
}
