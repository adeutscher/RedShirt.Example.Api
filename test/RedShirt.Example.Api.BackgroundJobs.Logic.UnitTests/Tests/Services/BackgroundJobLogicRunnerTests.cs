using Microsoft.Extensions.Logging.Abstractions;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Services;

public class BackgroundJobLogicRunnerTests
{
    [Fact]
    public async Task RunAsync_DispatchesToHandlerForType()
    {
        var job = JobFactory.Create(BackgroundJobType.ValidateUpload, "{}");
        var handler = new Mock<IBackgroundJobHandler>();
        handler.SetupGet(x => x.Type).Returns(BackgroundJobType.ValidateUpload);
        handler.Setup(x => x.HandleAsync(job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobLogicRunnerResponse.Success);
        var runner = new BackgroundJobLogicRunner([handler.Object], StatusServiceFactory.CreateAccepting().Object,
            NullLogger<BackgroundJobLogicRunner>.Instance);

        var response = await runner.RunAsync(job, TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.Success, response.Result);
        handler.Verify(x => x.HandleAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithoutHandlerForType_ReportsFailureWithoutRetry_AndReturnsInvalidData()
    {
        var job = JobFactory.Create(BackgroundJobType.ValidateUpload, "{}");
        var statusService = StatusServiceFactory.CreateAccepting();
        var runner = new BackgroundJobLogicRunner([], statusService.Object,
            NullLogger<BackgroundJobLogicRunner>.Instance);

        var response = await runner.RunAsync(job, TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.InvalidData, response.Result);
        var detail = ((JobLogicRunnerResponse) response).Detail;
        Assert.Contains(nameof(BackgroundJobType.ValidateUpload), detail);
        statusService.Verify(x => x.ReportStartedAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        statusService.Verify(x => x.ReportFailedAsync(job, false, detail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithoutHandlerForType_WhenStartIsRefused_DoesNotReportFailure()
    {
        var job = JobFactory.Create(BackgroundJobType.ValidateUpload, "{}");
        var statusService = new Mock<IBackgroundJobStatusService>();
        statusService.Setup(x => x.ReportStartedAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var runner = new BackgroundJobLogicRunner([], statusService.Object,
            NullLogger<BackgroundJobLogicRunner>.Instance);

        await runner.RunAsync(job, TestContext.Current.CancellationToken);

        statusService.Verify(x => x.ReportFailedAsync(It.IsAny<IJobModel>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}