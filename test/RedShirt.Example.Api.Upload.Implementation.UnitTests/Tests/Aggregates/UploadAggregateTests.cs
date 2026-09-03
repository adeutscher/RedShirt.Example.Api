using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Aggregates;

public class UploadAggregateTests
{
    [Fact]
    public void ResolveDownloadObjectKey_WhenNotStored_ReturnsStorageObjectKey()
    {
        var uploadId = Guid.NewGuid();
        const string storageKey = "user-id/upload-id";
        var aggregate = UploadAggregateTestSupport.Rehydrate(
            uploadId,
            UploadAggregateTestSupport.Created(uploadId),
            UploadAggregateTestSupport.Completed(uploadId, storageKey),
            UploadAggregateTestSupport.Validated(uploadId));

        Assert.Equal(storageKey, aggregate.ResolveDownloadObjectKey());
    }

    [Fact]
    public void ResolveDownloadObjectKey_WhenStored_ReturnsVerifiedStorageObjectKey()
    {
        var uploadId = Guid.NewGuid();
        const string unverifiedKey = "user-id/upload-id";
        const string verifiedKey = "verified/user-id/upload-id";
        var aggregate = UploadAggregateTestSupport.Rehydrate(
            uploadId,
            UploadAggregateTestSupport.Created(uploadId),
            UploadAggregateTestSupport.Completed(uploadId, unverifiedKey),
            UploadAggregateTestSupport.Validated(uploadId),
            UploadAggregateTestSupport.Stored(uploadId, verifiedKey));

        Assert.Equal(verifiedKey, aggregate.ResolveDownloadObjectKey());
    }

    [Fact]
    public void ToInternalDetailsModel_MapsAggregateFields()
    {
        var uploadId = Guid.NewGuid();
        const string storageKey = "user-id/upload-id";
        var aggregate = UploadAggregateTestSupport.Rehydrate(
            uploadId,
            UploadAggregateTestSupport.Created(uploadId),
            UploadAggregateTestSupport.Completed(uploadId, storageKey, "sha256-value"));

        var details = aggregate.ToInternalDetailsModel();

        Assert.Equal(uploadId, details.Id);
        Assert.Equal("user-id", details.UploadedByUserId);
        Assert.Equal("203.0.113.10", details.UploaderIpAddress);
        Assert.Equal(storageKey, details.StorageObjectKey);
        Assert.Equal("sha256-value", details.Sha256Checksum);
        Assert.Null(details.VerifiedStorageObjectKey);
    }

    [Fact]
    public void UsesVerifiedBucket_WhenStored_ReturnsTrue()
    {
        var uploadId = Guid.NewGuid();
        var aggregate = UploadAggregateTestSupport.Rehydrate(
            uploadId,
            UploadAggregateTestSupport.Created(uploadId),
            UploadAggregateTestSupport.Completed(uploadId, "user-id/upload-id"),
            UploadAggregateTestSupport.Validated(uploadId),
            UploadAggregateTestSupport.Stored(uploadId, "verified/user-id/upload-id"));

        Assert.True(aggregate.UsesVerifiedBucket());
        Assert.Equal(UploadState.Stored, aggregate.State);
    }
}