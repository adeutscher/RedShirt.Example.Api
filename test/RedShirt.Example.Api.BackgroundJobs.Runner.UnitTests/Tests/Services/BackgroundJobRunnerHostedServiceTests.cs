using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Runner.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Runner.UnitTests.Tests.Services;

public class BackgroundJobRunnerHostedServiceTests
{
    private readonly Mock<IJobLogicRunner> _jobLogicRunner = new();
    private readonly Mock<IBackgroundJobSource> _jobSource = new();
    private readonly Mock<IBackgroundJobWakeSignal> _wakeSignal = new();

    private static JobModel CreateJob()
    {
        return new JobModel
        {
            MessageId = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            Data = new JobDataModel
            {
                Type = BackgroundJobType.ValidateUpload,
                ParametersJson = "{}"
            }
        };
    }

    private BackgroundJobRunnerHostedService CreateService(BackgroundJobRunnerOptions? options = null)
    {
        var provider = new ServiceCollection()
            .AddSingleton(_jobSource.Object)
            .AddSingleton(_jobLogicRunner.Object)
            .BuildServiceProvider();

        return new BackgroundJobRunnerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _wakeSignal.Object,
            Options.Create(options ?? new BackgroundJobRunnerOptions {FetchCount = 3, MaxIdleWaitSeconds = 30}),
            NullLogger<BackgroundJobRunnerHostedService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdle_WaitsOnWakeSignal_AndKeepsPollingAfterSourceErrors()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        _jobSource.Setup(x => x.GetJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++polls == 1
                ? Task.FromException<IReadOnlyList<IJobModel>>(new InvalidOperationException("database unavailable"))
                : Task.FromResult<IReadOnlyList<IJobModel>>([]));
        var waits = new List<TimeSpan>();
        _wakeSignal.Setup(x => x.WaitAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>(async (delay, _) =>
            {
                waits.Add(delay);
                if (waits.Count == 3)
                {
                    await cancellation.CancelAsync();
                }

                return false;
            });
        var service = CreateService();

        await service.StartAsync(cancellation.Token);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(3, polls);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)], waits);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunnerDisabled_NeverFetches()
    {
        var service = CreateService(new BackgroundJobRunnerOptions {RunnerEnabled = false});

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.ExecuteTask!;
        await service.StopAsync(TestContext.Current.CancellationToken);

        _jobSource.Verify(x => x.GetJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(5, 30)]
    [InlineData(100, 30)]
    public void GetIdleDelay_DoublesUpToMaximum(int idleRounds, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), CreateService().GetIdleDelay(idleRounds));
    }

    [Fact]
    public async Task RunBatchAsync_RunsEachJobInOrder_AndDiscardsTheResponses()
    {
        var first = CreateJob();
        var second = CreateJob();
        var order = new List<string>();
        _jobSource.Setup(x => x.GetJobsAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        _jobLogicRunner.Setup(x => x.RunAsync(first, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("run-1"))
            .ReturnsAsync(JobLogicRunnerResponse.Success);
        _jobLogicRunner.Setup(x => x.RunAsync(second, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("run-2"))
            .ReturnsAsync(new JobLogicRunnerResponse {Result = JobResult.Failure, Detail = "nope"});

        var count = await CreateService().RunBatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        Assert.Equal(["run-1", "run-2"], order);
        // The job source is only asked for jobs: the job logic reports each job's status itself.
        _jobSource.Verify(x => x.GetJobsAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        _jobSource.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunBatchAsync_WhenCancelledDuringJob_StopsTheBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var first = CreateJob();
        var second = CreateJob();
        _jobSource.Setup(x => x.GetJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        _jobLogicRunner.Setup(x => x.RunAsync(first, It.IsAny<CancellationToken>()))
            .Returns<IJobModel, CancellationToken>(async (_, token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return JobLogicRunnerResponse.Success;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().RunBatchAsync(cancellation.Token));

        _jobLogicRunner.Verify(x => x.RunAsync(second, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_WhenJobThrows_ContinuesWithNextJob()
    {
        var first = CreateJob();
        var second = CreateJob();
        _jobSource.Setup(x => x.GetJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        _jobLogicRunner.Setup(x => x.RunAsync(first, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status report failed"));
        _jobLogicRunner.Setup(x => x.RunAsync(second, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobLogicRunnerResponse.Success);

        var count = await CreateService().RunBatchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        _jobLogicRunner.Verify(x => x.RunAsync(second, It.IsAny<CancellationToken>()), Times.Once);
    }
}