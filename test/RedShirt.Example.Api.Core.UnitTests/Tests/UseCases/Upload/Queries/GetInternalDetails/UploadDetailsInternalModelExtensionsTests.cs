using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetInternalDetails;

public class UploadDetailsInternalModelExtensionsTests
{
    [Fact]
    public void ToPublicDetailsModel_OmitsInternalFields()
    {
        var details = new UploadDetailsInternalModel
        {
            Id = Guid.NewGuid(),
            DateCreatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            StorageObjectKey = "upload-id/user-id",
            VerifiedStorageObjectKey = "verified/upload-id/user-id",
            DateStoredUtc = DateTime.UtcNow,
            Sha256Checksum = "abc123"
        };

        var result = details.ToPublicDetailsModel();

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(details.FileName, result.FileName);
        Assert.Equal(details.Sha256Checksum, result.Sha256Checksum);
        Assert.Equal(details.DateStoredUtc, result.DateStoredUtc);
    }

    [Fact]
    public void ToInternalDetailsModel_WhenNotStored_UsesStorageObjectKey()
    {
        var details = new UploadDetailsInternalModel
        {
            Id = Guid.NewGuid(),
            DateCreatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            StorageObjectKey = "upload-id/user-id",
            VerifiedStorageObjectKey = null
        };

        var result = details.ToInternalDetailsModel();

        Assert.Equal("203.0.113.10", result.UploaderIpAddress);
        Assert.Equal("upload-id/user-id", result.StorageObjectKey);
    }

    [Fact]
    public void ToInternalDetailsModel_WhenStored_UsesVerifiedStorageObjectKey()
    {
        var details = new UploadDetailsInternalModel
        {
            Id = Guid.NewGuid(),
            DateCreatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            StorageObjectKey = "upload-id/user-id",
            VerifiedStorageObjectKey = "upload-id/user-id",
            DateStoredUtc = DateTime.UtcNow
        };

        var result = details.ToInternalDetailsModel();

        Assert.Equal("203.0.113.10", result.UploaderIpAddress);
        Assert.Equal("upload-id/user-id", result.StorageObjectKey);
    }
}
