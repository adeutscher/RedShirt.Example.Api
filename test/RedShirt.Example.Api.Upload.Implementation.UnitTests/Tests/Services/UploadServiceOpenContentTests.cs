using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Common.FileStorage.Services;
using RedShirt.Example.Api.Upload.Core.Configuration;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Aggregates;
using RedShirt.Example.Api.Upload.Implementation.Repositories;
using RedShirt.Example.Api.Upload.Implementation.Services;
using RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Services;

public class UploadServiceOpenContentTests
{
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IUploadRepository> _repository = new();

    private UploadService CreateService(UploadAggregate aggregate)
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
                BucketVerifiedItems = "verified-uploads"
            }));
    }

    [Fact]
    public async Task OpenContentAsync_ForNotValidatedUpload_ReadsUnverifiedBucket()
    {
        await using var content = new MemoryStream();
        _fileStorage.Setup(x => x.OpenReadAsync("unverified-uploads", "user-id/key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var service = CreateService(UploadAggregateTestSupport.Rehydrate(UploadId,
            UploadAggregateTestSupport.Created(UploadId),
            UploadAggregateTestSupport.Completed(UploadId, "user-id/key")));

        var result = await service.OpenContentAsync(UploadId, TestContext.Current.CancellationToken);

        Assert.Same(content, result);
    }

    [Fact]
    public async Task OpenContentAsync_ForStoredUpload_ReadsVerifiedBucket()
    {
        await using var content = new MemoryStream();
        _fileStorage.Setup(x => x.OpenReadAsync("verified-uploads", "verified/key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var service = CreateService(UploadAggregateTestSupport.Rehydrate(UploadId,
            UploadAggregateTestSupport.Created(UploadId),
            UploadAggregateTestSupport.Completed(UploadId, "user-id/key"),
            UploadAggregateTestSupport.Validated(UploadId),
            UploadAggregateTestSupport.Stored(UploadId, "verified/key")));

        var result = await service.OpenContentAsync(UploadId, TestContext.Current.CancellationToken);

        Assert.Same(content, result);
    }

    [Fact]
    public async Task OpenContentAsync_WhileUploading_ThrowsBadRequest()
    {
        var service = CreateService(UploadAggregateTestSupport.Rehydrate(UploadId,
            UploadAggregateTestSupport.Created(UploadId)));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.OpenContentAsync(UploadId, TestContext.Current.CancellationToken));
        _fileStorage.Verify(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OpenContentAsync_WhenMissing_ThrowsResourceNotFound()
    {
        var service = CreateService(UploadAggregateTestSupport.Rehydrate(UploadId));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.OpenContentAsync(UploadId, TestContext.Current.CancellationToken));
    }
}
