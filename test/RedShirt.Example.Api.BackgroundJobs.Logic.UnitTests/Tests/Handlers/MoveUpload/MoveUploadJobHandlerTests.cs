using Microsoft.Extensions.Logging.Abstractions;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Handlers.MoveUpload;

public class MoveUploadJobHandlerTests
{
    private const string VerifiedKey = "user-id/key";
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IUploadStorageService> _service = new(MockBehavior.Strict);

    private static JobModel CreateJob(string? parametersJson = null)
    {
        return JobFactory.Create(BackgroundJobType.MoveUpload, parametersJson ?? $"{{\"uploadId\":\"{UploadId}\"}}");
    }

    private Task<IJobLogicRunnerResponse> HandleAsync(JobModel? job = null)
    {
        return new MoveUploadJobHandler(_service.Object, NullLogger<MoveUploadJobHandler>.Instance)
            .HandleAsync(job ?? CreateJob(), TestContext.Current.CancellationToken);
    }

    private void SetupStage(UploadMoveStage stage)
    {
        _service.Setup(x => x.GetMoveStageAsync(UploadId, It.IsAny<CancellationToken>())).ReturnsAsync(stage);
    }

    [Fact]
    public void Type_IsMoveUpload()
    {
        Assert.Equal(BackgroundJobType.MoveUpload,
            new MoveUploadJobHandler(_service.Object, NullLogger<MoveUploadJobHandler>.Instance).Type);
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
    public async Task HandleAsync_WhenReportFails_DoesNotRemoveUnverifiedCopy()
    {
        SetupStage(UploadMoveStage.AwaitingMove);
        _service.Setup(x => x.CopyToVerifiedStorageAsync(UploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VerifiedKey);
        _service.Setup(x => x.SubmitMoveReportAsync(UploadId, VerifiedKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => HandleAsync());
        // Strict mock: a RemoveUnverifiedCopyAsync call would have thrown a MockException instead.
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
    public async Task HandleAsync_WhenNotApplicable_SucceedsWithoutChanges()
    {
        SetupStage(UploadMoveStage.NotApplicable);

        var response = await HandleAsync();

        Assert.Equal(JobResult.Success, response.Result);
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
}
