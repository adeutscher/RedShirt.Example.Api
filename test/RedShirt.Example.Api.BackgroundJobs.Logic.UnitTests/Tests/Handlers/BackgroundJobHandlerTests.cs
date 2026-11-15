using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Handlers;

public class BackgroundJobHandlerTests
{
    private readonly Mock<IBackgroundJobStatusService> _statusService = StatusServiceFactory.CreateAccepting();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JobModel CreateJob(string parametersJson = "{\"uploadId\":\"00000000-0000-0000-0000-000000000001\"}")
    {
        return JobFactory.Create(BackgroundJobType.ValidateUpload, parametersJson);
    }

    private TestHandler CreateHandler(Func<CancellationToken, Task<IJobLogicRunnerResponse>> run)
    {
        return new TestHandler(_statusService.Object, run);
    }

    private void VerifyNoOutcomeReported()
    {
        _statusService.Verify(x => x.ReportFinishedAsync(It.IsAny<IJobModel>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _statusService.Verify(x => x.ReportFailedAsync(It.IsAny<IJobModel>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OnFailure_ReportsRetryableFailure()
    {
        var job = CreateJob();
        var handler = CreateHandler(_ => Task.FromResult<IJobLogicRunnerResponse>(
            new JobLogicRunnerResponse {Result = JobResult.Failure, Detail = "later"}));

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.Failure, response.Result);
        _statusService.Verify(x => x.ReportFailedAsync(job, true, "later", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OnInvalidData_ReportsFailureWithoutRetry()
    {
        var job = CreateJob();
        var handler = CreateHandler(_ => Task.FromResult<IJobLogicRunnerResponse>(
            new JobLogicRunnerResponse {Result = JobResult.InvalidData, Detail = "never"}));

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.InvalidData, response.Result);
        _statusService.Verify(x => x.ReportFailedAsync(job, false, "never", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OnSuccess_ReportsStartThenFinish()
    {
        var job = CreateJob();
        var order = new List<string>();
        _statusService.Setup(x => x.ReportStartedAsync(job, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("start"))
            .ReturnsAsync(true);
        _statusService.Setup(x => x.ReportFinishedAsync(job, "done", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("finish"))
            .ReturnsAsync(true);
        var handler = CreateHandler(_ =>
        {
            order.Add("run");
            return Task.FromResult<IJobLogicRunnerResponse>(
                new JobLogicRunnerResponse {Result = JobResult.Success, Detail = "done"});
        });

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.Success, response.Result);
        Assert.Equal(["start", "run", "finish"], order);
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_ReportsNoOutcome()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = CreateHandler(async token =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            return JobLogicRunnerResponse.Success;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(CreateJob(), cancellation.Token));

        VerifyNoOutcomeReported();
    }

    [Fact]
    public async Task HandleAsync_WhenFinishIsRefused_StillReturnsResponse()
    {
        var job = CreateJob();
        _statusService.Setup(x => x.ReportFinishedAsync(job, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler(_ => Task.FromResult<IJobLogicRunnerResponse>(JobLogicRunnerResponse.Success));

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.Success, response.Result);
    }

    [Fact]
    public async Task HandleAsync_WhenJobThrows_ReportsRetryableFailureWithDetail()
    {
        var job = CreateJob();
        var handler = CreateHandler(_ => throw new InvalidOperationException("boom"));

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.Failure, response.Result);
        Assert.Contains("boom", ((JobLogicRunnerResponse) response).Detail);
        _statusService.Verify(x => x.ReportFailedAsync(job, true,
            It.Is<string?>(detail => detail!.Contains("boom")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStartIsRefused_DoesNotRunOrReportOutcome()
    {
        var job = CreateJob();
        _statusService.Setup(x => x.ReportStartedAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var ran = false;
        var handler = CreateHandler(_ =>
        {
            ran = true;
            return Task.FromResult<IJobLogicRunnerResponse>(JobLogicRunnerResponse.Success);
        });

        var response = await handler.HandleAsync(job, Token);

        Assert.False(ran);
        Assert.Equal(JobResult.Success, response.Result);
        VerifyNoOutcomeReported();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task HandleAsync_WithUnreadableParameters_ReportsFailureWithoutRetry(string parametersJson)
    {
        var job = CreateJob(parametersJson);
        var handler = CreateHandler(_ => throw new InvalidOperationException("must not run"));

        var response = await handler.HandleAsync(job, Token);

        Assert.Equal(JobResult.InvalidData, response.Result);
        _statusService.Verify(x => x.ReportFailedAsync(job, false, It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestHandler(
        IBackgroundJobStatusService statusService,
        Func<CancellationToken, Task<IJobLogicRunnerResponse>> run)
        : BackgroundJobHandler<ValidateUploadJobParameters>(statusService)
    {
        protected override Task<IJobLogicRunnerResponse> HandleAsync(ValidateUploadJobParameters parameters,
            IJobModel job, CancellationToken cancellationToken)
        {
            return run(cancellationToken);
        }
    }
}
