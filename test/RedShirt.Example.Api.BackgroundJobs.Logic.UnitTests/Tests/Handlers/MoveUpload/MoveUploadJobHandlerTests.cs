using Microsoft.Extensions.Logging.Abstractions;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Handlers.MoveUpload;

public class MoveUploadJobHandlerTests
{
    private const string VerifiedKey = "user-id/key";
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IUploadStorageService> _service = new(MockBehavior.Strict);
    private readonly Mock<IBackgroundJobStatusService> _statusService = StatusServiceFactory.CreateAccepting();

    private static JobModel CreateJob(string? parametersJson = null)
    {
        return JobFactory.Create(BackgroundJobType.MoveUpload, parametersJson ?? $"{{\"uploadId\":\"{UploadId}\"}}");
    }

    private Task<IJobLogicRunnerResponse> HandleAsync(JobModel? job = null)
    {
        return new MoveUploadJobHandler(_service.Object, _statusService.Object,
                NullLogger<MoveUploadJobHandler>.Instance)
            .HandleAsync(job ?? CreateJob(), TestContext.Current.CancellationToken);
    }

    private void SetupStage(UploadMoveStage stage)
    {
        _service.Setup(x => x.GetMoveStageAsync(UploadId, It.IsAny<CancellationToken>())).ReturnsAsync(stage);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyStored_OnlyRemovesUnverifiedCopy()
    {
        SetupStage(UploadMoveStage.Stored);
        _service.Setup(x => x.RemoveUnverifiedCopyAsync(UploadId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await HandleAsync();

        Assert.Equal(JobResult.Success, response.Result);
        _service.Verify(x => x.RemoveUnverifiedCopyAsync(UploadId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAwaitingMove_CopiesThenReportsThenRemovesUnverifiedCopy()
    {
        var order = new List<string>();
        SetupStage(UploadMoveStage.AwaitingMove);
        _service.Setup(x => x.CopyToVerifiedStorageAsync(UploadId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("copy"))
            .ReturnsAsync(VerifiedKey);
        _service.Setup(x => x.SubmitMoveReportAsync(UploadId, VerifiedKey, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("report"))
            .Returns(Task.CompletedTask);
        _service.Setup(x => x.RemoveUnverifiedCopyAsync(UploadId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("remove"))
            .Returns(Task.CompletedTask);

        var response = await HandleAsync();

        Assert.Equal(JobResult.Success, response.Result);
        Assert.Equal(["copy", "report", "remove"], order);
    }

    [Fact]
    public async Task HandleAsync_WhenNotApplicable_SucceedsWithoutChanges()
    {
        SetupStage(UploadMoveStage.NotApplicable);

        var response = await HandleAsync();

        Assert.Equal(JobResult.Success, response.Result);
    }

    [Fact]
    public async Task HandleAsync_WhenReportFails_ReportsRetryableFailure_WithoutRemovingUnverifiedCopy()
    {
        SetupStage(UploadMoveStage.AwaitingMove);
        _service.Setup(x => x.CopyToVerifiedStorageAsync(UploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VerifiedKey);
        _service.Setup(x => x.SubmitMoveReportAsync(UploadId, VerifiedKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var response = await HandleAsync();

        // Strict mock: a RemoveUnverifiedCopyAsync call would have thrown a MockException instead.
        Assert.Equal(JobResult.Failure, response.Result);
        _statusService.Verify(x => x.ReportFailedAsync(It.IsAny<IJobModel>(), true,
                It.Is<string?>(detail => detail!.Contains("database unavailable")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"uploadId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task HandleAsync_WithUnusableParameters_ReturnsInvalidData(string parametersJson)
    {
        var response = await HandleAsync(CreateJob(parametersJson));

        Assert.Equal(JobResult.InvalidData, response.Result);
    }

    [Fact]
    public void Type_IsMoveUpload()
    {
        Assert.Equal(BackgroundJobType.MoveUpload,
            new MoveUploadJobHandler(_service.Object, _statusService.Object, NullLogger<MoveUploadJobHandler>.Instance)
                .Type);
    }
}