using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Common.FileStorage.Services;
using RedShirt.Example.Api.Upload.Core.Configuration;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Aggregates;
using RedShirt.Example.Api.Upload.Implementation.Repositories;
using RedShirt.Example.Api.Upload.Implementation.Services;
using RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Services;

public class UploadServiceTests
{
    private static UploadService CreateService(
        Mock<IUploadRepository> repository,
        Mock<IFileStorageService>? fileStorage = null,
        Mock<IUploadEventBroadcaster>? eventBroadcaster = null,
        UploadOptions? options = null)
    {
        return new UploadService(
            repository.Object,
            (fileStorage ?? new Mock<IFileStorageService>()).Object,
            (eventBroadcaster ?? new Mock<IUploadEventBroadcaster>()).Object,
            Options.Create(options ?? new UploadOptions
            {
                BucketUnverifiedItems = "unverified-uploads",
                BucketVerifiedItems = "verified-uploads"
            }));
    }

    private static UploadServiceCreateRequest CreateUploadRequest(long contentLength)
    {
        return new UploadServiceCreateRequest
        {
            FileName = "file.txt",
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            Content = Stream.Null,
            ContentLength = contentLength,
            IdempotencyKey = "idem-key"
        };
    }

    [Fact]
    public async Task CreateAsync_WhenContentLengthExceedsMaxUploadSize_ThrowsRequestTooLargeException()
    {
        var repository = new Mock<IUploadRepository>();
        var fileStorage = new Mock<IFileStorageService>();
        var service = CreateService(
            repository,
            fileStorage,
            options: new UploadOptions
            {
                BucketUnverifiedItems = "unverified-uploads",
                BucketVerifiedItems = "verified-uploads",
                MaxUploadSizeBytes = 1024
            });

        var exception = await Assert.ThrowsAsync<RequestTooLargeException>(() =>
            service.CreateAsync(CreateUploadRequest(2048), TestContext.Current.CancellationToken));

        Assert.Contains("1024", exception.Message);
        repository.Verify(
            x => x.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(
            x => x.AppendEventAsync(
                It.IsAny<Guid>(),
                It.IsAny<UploadEventType>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fileStorage.Verify(
            x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDetailsAsync_WhenAggregateExists_ReturnsInternalDetailsModel()
    {
        var uploadId = Guid.NewGuid();
        const string storageKey = "user-id/upload-id";
        var aggregate = UploadAggregateTestSupport.Rehydrate(
            uploadId,
            UploadAggregateTestSupport.Created(uploadId),
            UploadAggregateTestSupport.Completed(uploadId, storageKey));
        var repository = new Mock<IUploadRepository>();
        repository
            .Setup(x => x.GetAggregateFromEventsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);
        var service = CreateService(repository);

        var result = await service.GetDetailsAsync(uploadId, TestContext.Current.CancellationToken);

        Assert.Equal(uploadId, result.Id);
        Assert.Equal(storageKey, result.StorageObjectKey);
        Assert.Equal("203.0.113.10", result.UploaderIpAddress);
    }

    [Fact]
    public async Task GetDetailsAsync_WhenAggregateMissing_ThrowsResourceNotFoundException()
    {
        var uploadId = Guid.NewGuid();
        var repository = new Mock<IUploadRepository>();
        repository
            .Setup(x => x.GetAggregateFromEventsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadAggregate());
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.GetDetailsAsync(uploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PurgeAsync_DeletesBothStorageKeysAndPurgesRecords()
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
        var repository = new Mock<IUploadRepository>();
        repository
            .Setup(x => x.GetAggregateFromEventsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregate);
        repository
            .Setup(x => x.PurgeAsync(uploadId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var fileStorage = new Mock<IFileStorageService>();
        var eventBroadcaster = new Mock<IUploadEventBroadcaster>();
        eventBroadcaster
            .Setup(x => x.BroadcastUploadPurgedAsync(
                It.IsAny<UploadPurgedEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository, fileStorage, eventBroadcaster);

        await service.PurgeAsync(uploadId, TestContext.Current.CancellationToken);

        fileStorage.Verify(
            x => x.DeleteAsync("unverified-uploads", unverifiedKey, It.IsAny<CancellationToken>()),
            Times.Once);
        fileStorage.Verify(
            x => x.DeleteAsync("verified-uploads", verifiedKey, It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(x => x.PurgeAsync(uploadId, It.IsAny<CancellationToken>()), Times.Once);
        eventBroadcaster.Verify(
            x => x.BroadcastUploadPurgedAsync(
                It.Is<UploadPurgedEvent>(e => e.UploadId == uploadId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PurgeAsync_WhenAggregateMissing_ThrowsResourceNotFoundException()
    {
        var uploadId = Guid.NewGuid();
        var repository = new Mock<IUploadRepository>();
        repository
            .Setup(x => x.GetAggregateFromEventsAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadAggregate());
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.PurgeAsync(uploadId, TestContext.Current.CancellationToken));
    }
}