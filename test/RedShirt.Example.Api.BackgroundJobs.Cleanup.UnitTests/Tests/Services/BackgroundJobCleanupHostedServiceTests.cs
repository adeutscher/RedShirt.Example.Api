using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Cleanup.UnitTests.Tests.Services;

public class BackgroundJobCleanupHostedServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IDeleteBackgroundJobCommandHandler> _deleteHandler = new();
    private readonly Mock<ISearchBackgroundJobRecordsQueryHandler> _searchHandler = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static BackgroundJobModel CreateJob(BackgroundJobState state)
    {
        return new BackgroundJobModel
        {
            Id = Guid.NewGuid(),
            Type = BackgroundJobType.ValidateUpload,
            State = state,
            ParametersJson = "{}",
            Attempts = 1,
            DateCreatedUtc = Now.UtcDateTime.AddDays(-60),
            DateUpdatedUtc = Now.UtcDateTime.AddDays(-60),
            DueAtUtc = Now.UtcDateTime.AddDays(-60),
            CompletedAtUtc = Now.UtcDateTime.AddDays(-60)
        };
    }

    private static BackgroundJobSearchResponse Page(params BackgroundJobModel[] records)
    {
        return new BackgroundJobSearchResponse {Records = records};
    }

    private BackgroundJobCleanupHostedService CreateService(BackgroundJobCleanupOptions? options = null)
    {
        var provider = new ServiceCollection()
            .AddSingleton(_searchHandler.Object)
            .AddSingleton(_deleteHandler.Object)
            .BuildServiceProvider();

        return new BackgroundJobCleanupHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now),
            Options.Create(options ?? new BackgroundJobCleanupOptions
                {CleanupEnabled = true, CleanupRetentionDays = 30}),
            NullLogger<BackgroundJobCleanupHostedService>.Instance);
    }

    [Fact]
    public async Task CleanUpAsync_DeletesSucceededAndFailedJobsFinishedBeforeCutoff()
    {
        var succeeded = new[] {CreateJob(BackgroundJobState.Succeeded), CreateJob(BackgroundJobState.Succeeded)};
        var failed = CreateJob(BackgroundJobState.Failed);
        var queries = new List<SearchBackgroundJobRecordsQuery>();
        var pages = new Queue<BackgroundJobSearchResponse>([Page(succeeded), Page(), Page(failed), Page()]);
        _searchHandler.Setup(x => x.Handle(It.IsAny<SearchBackgroundJobRecordsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<SearchBackgroundJobRecordsQuery, CancellationToken>((query, _) => queries.Add(query))
            .ReturnsAsync(pages.Dequeue);

        var deleted = await CreateService().CleanUpAsync(Token);

        Assert.Equal(3, deleted);
        var cutoff = Now.UtcDateTime.AddDays(-30);
        Assert.Equal(
            [
                BackgroundJobState.Succeeded, BackgroundJobState.Succeeded, BackgroundJobState.Failed,
                BackgroundJobState.Failed
            ],
            queries.Select(x => x.State!.Value));
        Assert.All(queries, query =>
        {
            Assert.Equal(cutoff, query.CompletedBeforeUtc);
            Assert.Equal(BackgroundJobCleanupHostedService.PageSize, query.PageSize);
            Assert.Null(query.Type);
            Assert.Null(query.CreatedBeforeUtc);
        });
        foreach (var job in succeeded.Append(failed))
        {
            _deleteHandler.Verify(x => x.Handle(new DeleteBackgroundJobCommand(job.Id), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task CleanUpAsync_SkipsJobsThatChanged_AndStopsWhenAPageDeletesNothing()
    {
        var gone = CreateJob(BackgroundJobState.Succeeded);
        var retried = CreateJob(BackgroundJobState.Succeeded);
        var raced = CreateJob(BackgroundJobState.Succeeded);
        // The same page every time, as if the jobs could never be deleted.
        _searchHandler.Setup(x => x.Handle(It.IsAny<SearchBackgroundJobRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchBackgroundJobRecordsQuery query, CancellationToken _) =>
                query.State == BackgroundJobState.Succeeded ? Page(gone, retried, raced) : Page());
        _deleteHandler.Setup(x => x.Handle(new DeleteBackgroundJobCommand(gone.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());
        _deleteHandler.Setup(x => x.Handle(new DeleteBackgroundJobCommand(retried.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("not finished"));
        _deleteHandler.Setup(x => x.Handle(new DeleteBackgroundJobCommand(raced.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("changed"));

        var deleted = await CreateService().CleanUpAsync(Token);

        Assert.Equal(0, deleted);
        _searchHandler.Verify(x => x.Handle(It.IsAny<SearchBackgroundJobRecordsQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CleanUpAsync_WhenDeleteFailsUnexpectedly_Throws()
    {
        var job = CreateJob(BackgroundJobState.Failed);
        _searchHandler.Setup(x => x.Handle(It.IsAny<SearchBackgroundJobRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(job));
        _deleteHandler.Setup(x => x.Handle(It.IsAny<DeleteBackgroundJobCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CleanUpAsync(Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNothing()
    {
        using var service = CreateService(new BackgroundJobCleanupOptions {CleanupRetentionDays = 30});

        await service.StartAsync(Token);
        await service.ExecuteTask!;

        _searchHandler.VerifyNoOtherCalls();
        _deleteHandler.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}