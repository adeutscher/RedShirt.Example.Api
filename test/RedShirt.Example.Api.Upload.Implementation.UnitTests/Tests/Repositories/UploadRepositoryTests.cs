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
        string idempotencyKey,
        string sha256Checksum = "sha256",
        long fileSizeBytes = 1024)
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
                Sha256Checksum = sha256Checksum,
                FileSizeBytes = fileSizeBytes
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
                Sha256Checksum = "sha256",
                FileSizeBytes = 2048
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(UploadState.NotValidated, summary.State);
        Assert.Equal("sha256", summary.Sha256Checksum);
        Assert.Equal(2048, summary.FileSizeBytes);

        var aggregate = await repository.GetAggregateFromEventsAsync(uploadId, TestContext.Current.CancellationToken);
        Assert.Equal(UploadState.NotValidated, aggregate.State);
        Assert.Equal("user-id/upload-id", aggregate.StorageObjectKey);
        Assert.Equal("sha256", aggregate.Sha256Checksum);
        Assert.Equal(2048, aggregate.FileSizeBytes);

        var storedSummary = await repository.GetSummaryAsync(uploadId, TestContext.Current.CancellationToken);
        Assert.NotNull(storedSummary);
        Assert.Equal("sha256", storedSummary.Sha256Checksum);
        Assert.Equal(2048, storedSummary.FileSizeBytes);
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
    public async Task SearchAsync_FiltersByFileSizeBytes()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "small.txt", "idem-a", fileSizeBytes: 512);
        await SeedUploadAsync(repository, "user-a", "medium.txt", "idem-b", fileSizeBytes: 2048);
        await SeedUploadAsync(repository, "user-a", "large.txt", "idem-c", fileSizeBytes: 8192);

        var exactMatch = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                FileSizeBytes = 2048
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(exactMatch.Records);
        Assert.Equal("medium.txt", exactMatch.Records[0].FileName);

        var greaterThan = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                FileSizeBytesGreaterThan = 1024
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, greaterThan.Records.Count);
        Assert.Contains(greaterThan.Records, record => record.FileName == "medium.txt");
        Assert.Contains(greaterThan.Records, record => record.FileName == "large.txt");

        var lessThan = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                FileSizeBytesLessThan = 2048
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(lessThan.Records);
        Assert.Equal("small.txt", lessThan.Records[0].FileName);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySha256Checksum_ExactMatchCaseInsensitive()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a.txt", "idem-a", "abc123def456");
        await SeedUploadAsync(repository, "user-a", "b.txt", "idem-b", "fed654cba321");

        var lowercaseMatch = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                Sha256Checksum = "abc123def456"
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(lowercaseMatch.Records);
        Assert.Equal("a.txt", lowercaseMatch.Records[0].FileName);

        var uppercaseMatch = await repository.SearchAsync(
            new UploadServiceSearchRequest
            {
                PageSize = 10,
                Sha256Checksum = "FED654CBA321"
            },
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(uppercaseMatch.Records);
        Assert.Equal("b.txt", uppercaseMatch.Records[0].FileName);
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
    public async Task SearchAsync_IgnoresAnotherCallersContinuationToken()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a1.txt", "idem-a1");
        await SeedUploadAsync(repository, "user-a", "a2.txt", "idem-a2");
        await SeedUploadAsync(repository, "user-b", "b1.txt", "idem-b1");

        var userAFirstPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1, UploadedByUserId = "user-a"},
            null,
            TestContext.Current.CancellationToken);
        Assert.NotNull(userAFirstPage.ContinuationToken);

        // user-b's controller scopes the request to user-b, then presents user-a's token.
        var userBPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 10, UploadedByUserId = "user-b"},
            userAFirstPage.ContinuationToken,
            TestContext.Current.CancellationToken);

        var record = Assert.Single(userBPage.Records);
        Assert.Equal("user-b", record.UploadedByUserId);
    }

    [Fact]
    public async Task SearchAsync_IgnoresTokenIssuedByAnotherEntity()
    {
        var cache = new InMemoryRemoteCacheService();
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory, cache);
        await SeedUploadAsync(repository, "user-a", "a1.txt", "idem-a1");
        await SeedUploadAsync(repository, "user-b", "b1.txt", "idem-b1");

        // A product search's cached parameters: same member names, no uploader filter.
        var token = Guid.NewGuid();
        var productShaped = """
                            {"OrderBys":[],"SearchParameters":{"PageSize":10},"LastId":"00000000-0000-0000-0000-000000000000","LastUpdatedAtUtc":"2999-01-01T00:00:00Z"}
                            """;
        await cache.SetStringAsync($"continuation:product:{token}", productShaped, TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);
        await cache.SetStringAsync($"continuation:{token}", productShaped, TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        var page = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 10, UploadedByUserId = "user-b"},
            token,
            TestContext.Current.CancellationToken);

        var record = Assert.Single(page.Records);
        Assert.Equal("user-b", record.UploadedByUserId);
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

    [Fact]
    public async Task SearchAsync_SameCallerContinues_WhenScopedFilterIsRepeated()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a1.txt", "idem-a1");
        await SeedUploadAsync(repository, "user-a", "a2.txt", "idem-a2");
        await SeedUploadAsync(repository, "user-b", "b1.txt", "idem-b1");

        var firstPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1, UploadedByUserId = "user-a"},
            null,
            TestContext.Current.CancellationToken);
        var secondPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1, UploadedByUserId = "user-a"},
            firstPage.ContinuationToken,
            TestContext.Current.CancellationToken);

        var first = Assert.Single(firstPage.Records);
        var second = Assert.Single(secondPage.Records);
        Assert.Equal("user-a", second.UploadedByUserId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task SearchAsync_UnscopedTokenDoesNotWidenScopedSearch()
    {
        var repository = UploadRepositoryTestSupport.CreateRepository(_dbContextFactory);
        await SeedUploadAsync(repository, "user-a", "a1.txt", "idem-a1");
        await SeedUploadAsync(repository, "user-a", "a2.txt", "idem-a2");
        await SeedUploadAsync(repository, "user-b", "b1.txt", "idem-b1");

        // An unrestricted caller (no uploader filter) issues the token.
        var unrestrictedFirstPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 1},
            null,
            TestContext.Current.CancellationToken);
        Assert.NotNull(unrestrictedFirstPage.ContinuationToken);

        var scopedPage = await repository.SearchAsync(
            new UploadServiceSearchRequest {PageSize = 10, UploadedByUserId = "user-b"},
            unrestrictedFirstPage.ContinuationToken,
            TestContext.Current.CancellationToken);

        Assert.All(scopedPage.Records, r => Assert.Equal("user-b", r.UploadedByUserId));
        Assert.Single(scopedPage.Records);
    }
}