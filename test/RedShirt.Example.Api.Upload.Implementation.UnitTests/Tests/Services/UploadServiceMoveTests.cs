using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Common.FileStorage.Services;
using RedShirt.Example.Api.Upload.Core.Configuration;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Aggregates;
using RedShirt.Example.Api.Upload.Implementation.Repositories;
using RedShirt.Example.Api.Upload.Implementation.Services;
using RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Services;

public class UploadServiceMoveTests
{
    private const string Key = "user-id/key";
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IUploadRepository> _repository = new();

    private static UploadAggregate Verified => UploadAggregateTestSupport.Rehydrate(UploadId,
        UploadAggregateTestSupport.Created(UploadId),
        UploadAggregateTestSupport.Completed(UploadId, Key),
        UploadAggregateTestSupport.Validated(UploadId));

    private static UploadAggregate Stored => UploadAggregateTestSupport.Rehydrate(UploadId,
        UploadAggregateTestSupport.Created(UploadId),
        UploadAggregateTestSupport.Completed(UploadId, Key),
        UploadAggregateTestSupport.Validated(UploadId),
        UploadAggregateTestSupport.Stored(UploadId, Key));

    private UploadService CreateService(UploadAggregate aggregate, string verifiedBucket = "verified-uploads")
    {
        _repository.Setup(x => x.GetAggregateFromEventsAsync(UploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);
        return new UploadService(
            _repository.Object,
            _fileStorage.Object,
            new Mock<IUploadEventBroadcaster>().Object,
            Options.Create(new UploadOptions
            {
                BucketUnverifiedItems = "unverified-uploads",
                BucketVerifiedItems = verifiedBucket
            }));
    }

    [Fact]
    public async Task CopyToVerifiedStorageAsync_CopiesUnderSameKey_WithoutChangingState()
    {
        var result = await CreateService(Verified)
            .CopyToVerifiedStorageAsync(UploadId, TestContext.Current.CancellationToken);

        Assert.Equal(Key, result);
        _fileStorage.Verify(x => x.CopyAsync("unverified-uploads", Key, "verified-uploads", Key,
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(x => x.AppendEventAsync(It.IsAny<Guid>(), It.IsAny<UploadEventType>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CopyToVerifiedStorageAsync_WhenMissing_ThrowsResourceNotFound()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            CreateService(UploadAggregateTestSupport.Rehydrate(UploadId))
                .CopyToVerifiedStorageAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyToVerifiedStorageAsync_WhenNotVerified_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService(Stored).CopyToVerifiedStorageAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveUnverifiedCopyAsync_DeletesFromUnverifiedBucket()
    {
        await CreateService(Stored).RemoveUnverifiedCopyAsync(UploadId, TestContext.Current.CancellationToken);

        _fileStorage.Verify(x => x.DeleteAsync("unverified-uploads", Key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUnverifiedCopyAsync_WhenNotStored_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService(Verified).RemoveUnverifiedCopyAsync(UploadId, TestContext.Current.CancellationToken));
        _fileStorage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUnverifiedCopyAsync_WithOneBucketForBoth_KeepsTheStoredFile()
    {
        await CreateService(Stored, "unverified-uploads")
            .RemoveUnverifiedCopyAsync(UploadId, TestContext.Current.CancellationToken);

        _fileStorage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}