using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.UnitTests.Support;
using RedShirt.Example.Api.BackgroundJobs.Submission.Models;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.UnitTests.Tests.Services;

public class BackgroundJobServiceTests
{
    private const string Owner = "runner-a";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<BackgroundJobModel> CreateJobAsync(BackgroundJobServiceFixture fixture,
        string parametersJson = "{\"uploadId\":\"00000000-0000-0000-0000-000000000001\"}")
    {
        return fixture.Service.CreateAsync(new BackgroundJobServiceCreateRequest
        {
            Type = BackgroundJobType.ValidateUpload,
            ParametersJson = parametersJson
        }, Token);
    }

    private static Task<IReadOnlyList<BackgroundJobModel>> LeaseAsync(BackgroundJobServiceFixture fixture,
        int count = 10, string owner = Owner)
    {
        return fixture.Service.LeaseAsync(new BackgroundJobServiceLeaseRequest
        {
            Count = count,
            LeaseOwner = owner
        }, Token);
    }

    /// <summary>
    ///     Acknowledges with <paramref name="attempt" />, or by default with the job's current attempt number (the attempt
    ///     of the latest lease, when there is one).
    /// </summary>
    private static async Task<BackgroundJobModel> AcknowledgeAsync(BackgroundJobServiceFixture fixture, Guid id,
        BackgroundJobAttemptResult result, string owner = Owner, string? detail = null, int? attempt = null)
    {
        var resolvedAttempt = attempt ?? await GetCurrentAttemptAsync(fixture, id);
        return await fixture.Service.AcknowledgeAsync(new BackgroundJobServiceAcknowledgeRequest
        {
            Id = id,
            LeaseOwner = owner,
            Attempt = resolvedAttempt,
            Result = result,
            Detail = detail
        }, Token);
    }

    private static async Task<int> GetCurrentAttemptAsync(BackgroundJobServiceFixture fixture, Guid id)
    {
        try
        {
            return (await fixture.Service.GetAsync(id, Token)).Attempts;
        }
        catch (ResourceNotFoundException)
        {
            return 1;
        }
    }

    [Fact]
    public async Task CreateAsync_StoresPendingJob()
    {
        using var fixture = new BackgroundJobServiceFixture();

        var created = await CreateJobAsync(fixture);
        var fetched = await fixture.Service.GetAsync(created.Id, Token);

        Assert.Equal(BackgroundJobState.Pending, fetched.State);
        Assert.Equal(BackgroundJobType.ValidateUpload, fetched.Type);
        Assert.Equal(0, fetched.Attempts);
        Assert.Equal(created.ParametersJson, fetched.ParametersJson);
        Assert.Equal(fixture.TimeProvider.UtcNow.UtcDateTime, fetched.DueAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownType_ThrowsBadRequest()
    {
        using var fixture = new BackgroundJobServiceFixture();

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.CreateAsync(
            new BackgroundJobServiceCreateRequest
            {
                Type = (BackgroundJobType) 999,
                ParametersJson = "{}"
            }, Token));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyParameters_ThrowsBadRequest()
    {
        using var fixture = new BackgroundJobServiceFixture();

        await Assert.ThrowsAsync<BadRequestException>(() => CreateJobAsync(fixture, " "));
    }

    [Fact]
    public async Task GetAsync_WhenMissing_ThrowsResourceNotFound()
    {
        using var fixture = new BackgroundJobServiceFixture();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Service.GetAsync(Guid.NewGuid(), Token));
    }

    [Fact]
    public async Task LeaseAsync_LeasesPendingJobs_OldestFirst_UpToCount()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var first = await CreateJobAsync(fixture);
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var second = await CreateJobAsync(fixture);
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        await CreateJobAsync(fixture);

        var leased = await LeaseAsync(fixture, 2);

        Assert.Equal([first.Id, second.Id], leased.Select(x => x.Id));
        Assert.All(leased, job =>
        {
            Assert.Equal(BackgroundJobState.Running, job.State);
            Assert.Equal(1, job.Attempts);
            Assert.Equal(Owner, job.LeaseOwner);
            Assert.Equal(fixture.TimeProvider.UtcNow.UtcDateTime.AddSeconds(60), job.LeaseExpiresAtUtc);
        });
    }

    [Fact]
    public async Task LeaseAsync_DoesNotReleaseLeasedJob_UntilLeaseExpires()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        Assert.Empty(await LeaseAsync(fixture, owner: "runner-b"));

        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(61));
        var released = await LeaseAsync(fixture, owner: "runner-b");

        var single = Assert.Single(released);
        Assert.Equal(job.Id, single.Id);
        Assert.Equal("runner-b", single.LeaseOwner);
        Assert.Equal(2, single.Attempts);
    }

    [Fact]
    public async Task LeaseAsync_WhenFinalAttemptLeaseExpires_MarksJobFailed()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        for (var attempt = 0; attempt < fixture.Options.MaxAttempts; attempt++)
        {
            Assert.Single(await LeaseAsync(fixture));
            fixture.TimeProvider.Advance(TimeSpan.FromSeconds(61));
        }

        Assert.Empty(await LeaseAsync(fixture));

        var failed = await fixture.Service.GetAsync(job.Id, Token);
        Assert.Equal(BackgroundJobState.Failed, failed.State);
        Assert.NotNull(failed.CompletedAtUtc);
        Assert.Null(failed.LeaseOwner);
        Assert.NotNull(failed.Detail);
    }

    [Fact]
    public async Task LeaseAsync_WithNonPositiveCount_ReturnsEmpty()
    {
        using var fixture = new BackgroundJobServiceFixture();
        await CreateJobAsync(fixture);

        Assert.Empty(await LeaseAsync(fixture, 0));
    }

    [Fact]
    public async Task LeaseAsync_WithEmptyOwner_ThrowsBadRequest()
    {
        using var fixture = new BackgroundJobServiceFixture();

        await Assert.ThrowsAsync<BadRequestException>(() => LeaseAsync(fixture, owner: ""));
    }

    [Fact]
    public async Task AcknowledgeAsync_Success_MarksSucceeded()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        var acknowledged = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Success, detail: "Approved.");

        Assert.Equal(BackgroundJobState.Succeeded, acknowledged.State);
        Assert.Equal(fixture.TimeProvider.UtcNow.UtcDateTime, acknowledged.CompletedAtUtc);
        Assert.Null(acknowledged.LeaseOwner);
        Assert.Null(acknowledged.LeaseExpiresAtUtc);
        Assert.Equal("Approved.", acknowledged.Detail);
    }

    [Fact]
    public async Task AcknowledgeAsync_InvalidData_MarksFailed_WithoutRetry()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        var acknowledged = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.InvalidData, detail: "bad");

        Assert.Equal(BackgroundJobState.Failed, acknowledged.State);
        Assert.NotNull(acknowledged.CompletedAtUtc);
        Assert.Equal("bad", acknowledged.Detail);
    }

    [Fact]
    public async Task AcknowledgeAsync_Failure_WithAttemptsLeft_SchedulesRetryWithBackoff()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        var first = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Failure, detail: "boom");

        Assert.Equal(BackgroundJobState.Pending, first.State);
        Assert.Null(first.CompletedAtUtc);
        Assert.Null(first.LeaseOwner);
        Assert.Equal("boom", first.Detail);
        Assert.Equal(fixture.TimeProvider.UtcNow.UtcDateTime.AddSeconds(10), first.DueAtUtc);

        // Not due yet.
        Assert.Empty(await LeaseAsync(fixture));

        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        Assert.Single(await LeaseAsync(fixture));
        var second = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Failure);

        // 10 s doubled, within the 25 s cap.
        Assert.Equal(fixture.TimeProvider.UtcNow.UtcDateTime.AddSeconds(20), second.DueAtUtc);
    }

    [Fact]
    public async Task AcknowledgeAsync_Failure_OnFinalAttempt_MarksFailed()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);

        BackgroundJobModel? last = null;
        for (var attempt = 0; attempt < fixture.Options.MaxAttempts; attempt++)
        {
            fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
            Assert.Single(await LeaseAsync(fixture));
            last = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Failure);
        }

        Assert.Equal(BackgroundJobState.Failed, last!.State);
        Assert.Equal(fixture.Options.MaxAttempts, last.Attempts);
    }

    [Fact]
    public async Task AcknowledgeAsync_FromDifferentOwner_ThrowsConflict()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        await Assert.ThrowsAsync<ConflictException>(() =>
            AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Success, "runner-b"));
    }

    [Fact]
    public async Task AcknowledgeAsync_FromExpiredLease_WithSameOwnerName_ThrowsConflict()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        var firstLease = Assert.Single(await LeaseAsync(fixture));
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(61));
        // Another process with the same lease owner name takes the job after the first lease expires.
        var secondLease = Assert.Single(await LeaseAsync(fixture));

        await Assert.ThrowsAsync<ConflictException>(() =>
            AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Success, attempt: firstLease.Attempts));

        var stillRunning = await fixture.Service.GetAsync(job.Id, Token);
        Assert.Equal(BackgroundJobState.Running, stillRunning.State);

        var acknowledged = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Success,
            attempt: secondLease.Attempts);
        Assert.Equal(BackgroundJobState.Succeeded, acknowledged.State);
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenNotLeased_ThrowsConflict()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);

        await Assert.ThrowsAsync<ConflictException>(() => AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.Success));
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenMissing_ThrowsResourceNotFound()
    {
        using var fixture = new BackgroundJobServiceFixture();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            AcknowledgeAsync(fixture, Guid.NewGuid(), BackgroundJobAttemptResult.Success));
    }

    [Fact]
    public async Task AcknowledgeAsync_TrimsLongDetail()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);

        var acknowledged = await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.InvalidData,
            detail: new string('x', 5000));

        Assert.Equal(4000, acknowledged.Detail!.Length);
    }

    [Fact]
    public async Task RetryAsync_MakesFailedJobPendingWithFreshAttempts()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);
        await LeaseAsync(fixture);
        await AcknowledgeAsync(fixture, job.Id, BackgroundJobAttemptResult.InvalidData);

        var retried = await fixture.Service.RetryAsync(job.Id, Token);

        Assert.Equal(BackgroundJobState.Pending, retried.State);
        Assert.Equal(0, retried.Attempts);
        Assert.Null(retried.CompletedAtUtc);
        Assert.Single(await LeaseAsync(fixture));
    }

    [Fact]
    public async Task RetryAsync_WhenNotFailed_ThrowsBadRequest()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var job = await CreateJobAsync(fixture);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.RetryAsync(job.Id, Token));
    }

    [Fact]
    public async Task SearchAsync_FiltersByState_NewestFirst_AndPagesByCreationTime()
    {
        using var fixture = new BackgroundJobServiceFixture();
        var first = await CreateJobAsync(fixture);
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var second = await CreateJobAsync(fixture);
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var third = await CreateJobAsync(fixture);
        await LeaseAsync(fixture, 1);

        var pending = await fixture.Service.SearchAsync(new BackgroundJobServiceSearchRequest
        {
            State = BackgroundJobState.Pending
        }, Token);
        Assert.Equal([third.Id, second.Id], pending.Records.Select(x => x.Id));

        var firstPage = await fixture.Service.SearchAsync(new BackgroundJobServiceSearchRequest
        {
            PageSize = 2,
            Type = BackgroundJobType.ValidateUpload
        }, Token);
        Assert.Equal([third.Id, second.Id], firstPage.Records.Select(x => x.Id));

        var secondPage = await fixture.Service.SearchAsync(new BackgroundJobServiceSearchRequest
        {
            PageSize = 2,
            CreatedBeforeUtc = firstPage.Records[^1].DateCreatedUtc
        }, Token);
        Assert.Equal([first.Id], secondPage.Records.Select(x => x.Id));
    }
}
