using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Implementation.Repositories;
using RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Repositories;

public class UploadRepositoryTests : IDisposable
{
    private readonly SqliteUploadDbContextFactory _dbContextFactory = new();

    private static async Task SeedUploadAsync(
        UploadRepository repository,
        string uploadedByUserId,
        string fileName,
        string idempotencyKey)
    {
        var uploadId = Guid.NewGuid();
        await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Created,
            new UploadCreatedEvent
            {
                UploadId = uploadId,
                UploadedByUserId = uploadedByUserId,
                UploadedByUsername = "user",
                UploaderIpAddress = "203.0.113.10",
                FileName = fileName,
                IdempotencyKey = idempotencyKey
            },
            TestContext.Current.CancellationToken);
        await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Completed,
            new UploadCompletedEvent
            {
                UploadId = uploadId,
                StorageObjectKey = $"{uploadedByUserId}/{uploadId:N}",
                Sha256Checksum = "sha256"
            },
            TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _dbContextFactory.Dispose();
    }

    [Fact]
    public async Task AppendEventAsync_CompletedEvent_UpdatesAggregateState()
    {
        var uploadId = Guid.NewGuid();
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Created,
            new UploadCreatedEvent
            {
                UploadId = uploadId,
                UploadedByUserId = "user-id",
                UploadedByUsername = "user",
                UploaderIpAddress = "203.0.113.10",
                FileName = "file.txt",
                IdempotencyKey = "idem-key"
            },
            TestContext.Current.CancellationToken);

        var summary = await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Completed,
            new UploadCompletedEvent
            {
                UploadId = uploadId,
                StorageObjectKey = "user-id/upload-id",
                Sha256Checksum = "sha256"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(UploadState.NotValidated, summary.State);

        var aggregate = await repository.GetAggregateFromEventsAsync(uploadId, TestContext.Current.CancellationToken);
        Assert.Equal(UploadState.NotValidated, aggregate.State);
        Assert.Equal("user-id/upload-id", aggregate.StorageObjectKey);
    }

    [Fact]
    public async Task AppendEventAsync_CreatedEvent_PersistsAggregateAndEvent()
    {
        var uploadId = Guid.NewGuid();
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        var createdEvent = new UploadCreatedEvent
        {
            UploadId = uploadId,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            IdempotencyKey = "idem-key"
        };

        var summary = await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Created,
            createdEvent,
            TestContext.Current.CancellationToken);

        Assert.Equal(uploadId, summary.Id);
        Assert.Equal(UploadState.Uploading, summary.State);
        Assert.Equal("file.txt", summary.FileName);

        var storedSummary = await repository.GetSummaryAsync(uploadId, TestContext.Current.CancellationToken);
        Assert.NotNull(storedSummary);
        Assert.Equal("user-id", storedSummary.UploadedByUserId);

        var events = await repository.GetEventsAsync(uploadId, TestContext.Current.CancellationToken);
        Assert.Single(events);
        Assert.Equal(UploadEventType.Created, events[0].EventType);
    }

    [Fact]
    public async Task ExistsByIdempotencyKeyAsync_ReturnsTrueWhenKeyExists()
    {
        var uploadId = Guid.NewGuid();
        const string idempotencyKey = "shared-idempotency-key";
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Created,
            new UploadCreatedEvent
            {
                UploadId = uploadId,
                UploadedByUserId = "user-id",
                UploadedByUsername = "user",
                UploaderIpAddress = "203.0.113.10",
                FileName = "file.txt",
                IdempotencyKey = idempotencyKey
            },
            TestContext.Current.CancellationToken);

        var exists = await repository.ExistsByIdempotencyKeyAsync(
            idempotencyKey,
            TestContext.Current.CancellationToken);

        Assert.True(exists);
        Assert.False(
            await repository.ExistsByIdempotencyKeyAsync("missing-key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsNullWhenMissing()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);

        var summary = await repository.GetSummaryAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(summary);
    }

    [Fact]
    public async Task PurgeAsync_RemovesAggregateAndEvents()
    {
        var uploadId = Guid.NewGuid();
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await repository.AppendEventAsync(
            uploadId,
            UploadEventType.Created,
            new UploadCreatedEvent
            {
                UploadId = uploadId,
                UploadedByUserId = "user-id",
                UploadedByUsername = "user",
                UploaderIpAddress = "203.0.113.10",
                FileName = "file.txt",
                IdempotencyKey = "purge-idem"
            },
            TestContext.Current.CancellationToken);

        await repository.PurgeAsync(uploadId, TestContext.Current.CancellationToken);

        Assert.Null(await repository.GetSummaryAsync(uploadId, TestContext.Current.CancellationToken));
        Assert.Empty(await repository.GetEventsAsync(uploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PurgeAsync_WhenMissing_ThrowsResourceNotFoundException()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            repository.PurgeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_FiltersByUploadedByUserId()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a.txt", "idem-a");
        await SeedUploadAsync(repository, "user-b", "b.txt", "idem-b");

        var response = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                UploadedByUserId = "user-a"
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(response.Records);
        Assert.Equal("user-a", response.Records[0].UploadedByUserId);
        Assert.Null(response.ContinuationToken);
    }

    [Fact]
    public async Task SearchAsync_ReturnsContinuationTokenWhenPageIsFull()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a.txt", "idem-a");
        await SeedUploadAsync(repository, "user-a", "b.txt", "idem-b");

        var firstPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1},
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(firstPage.Records);
        Assert.NotNull(firstPage.ContinuationToken);

        var secondPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1},
            firstPage.ContinuationToken,
            TestContext.Current.CancellationToken);

        Assert.Single(secondPage.Records);
        Assert.NotEqual(firstPage.Records[0].Id, secondPage.Records[0].Id);
    }
}