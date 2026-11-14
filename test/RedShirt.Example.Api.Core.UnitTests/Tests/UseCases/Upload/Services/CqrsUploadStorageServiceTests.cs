using Moq;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.CopyToVerifiedStorage;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.RemoveUnverifiedCopy;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Core.UseCases.Upload.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Services;

public class CqrsUploadStorageServiceTests
{
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<ICopyUploadToVerifiedStorageCommandHandler> _copy = new();
    private readonly Mock<IGetUploadSummaryQueryHandler> _getSummary = new();
    private readonly Mock<IRemoveUnverifiedUploadCopyCommandHandler> _remove = new();
    private readonly Mock<ISubmitUploadMoveReportCommandHandler> _report = new();

    private CqrsUploadStorageService CreateService()
    {
        return new CqrsUploadStorageService(_getSummary.Object, _copy.Object, _report.Object, _remove.Object);
    }

    [Theory]
    [InlineData(UploadState.Verified, UploadMoveStage.AwaitingMove)]
    [InlineData(UploadState.Stored, UploadMoveStage.Stored)]
    [InlineData(UploadState.NotValidated, UploadMoveStage.NotApplicable)]
    [InlineData(UploadState.Rejected, UploadMoveStage.NotApplicable)]
    [InlineData(UploadState.Deleted, UploadMoveStage.NotApplicable)]
    public async Task GetMoveStageAsync_MapsUploadState(UploadState state, UploadMoveStage expected)
    {
        _getSummary.Setup(x => x.Handle(new GetUploadSummaryQuery(UploadId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSummaryModel
            {
                Id = UploadId,
                DateCreatedUtc = DateTime.UtcNow,
                DateUpdatedUtc = DateTime.UtcNow,
                UploadedByUserId = "user-id",
                State = state,
                FileName = "file.txt",
                IsValidated = false,
                IsRejected = false
            });

        Assert.Equal(expected, await CreateService().GetMoveStageAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMoveStageAsync_WhenUploadIsGone_IsNotApplicable()
    {
        _getSummary.Setup(x => x.Handle(It.IsAny<GetUploadSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException());

        Assert.Equal(UploadMoveStage.NotApplicable,
            await CreateService().GetMoveStageAsync(UploadId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OtherOperations_UseTheirCommands()
    {
        var token = TestContext.Current.CancellationToken;
        _copy.Setup(x => x.Handle(new CopyUploadToVerifiedStorageCommand(UploadId), It.IsAny<CancellationToken>()))
            .ReturnsAsync("key");
        var service = CreateService();

        Assert.Equal("key", await service.CopyToVerifiedStorageAsync(UploadId, token));
        await service.SubmitMoveReportAsync(UploadId, "key", token);
        await service.RemoveUnverifiedCopyAsync(UploadId, token);

        _report.Verify(x => x.Handle(new SubmitUploadMoveReportCommand(UploadId, "key"),
            It.IsAny<CancellationToken>()), Times.Once);
        _remove.Verify(x => x.Handle(new RemoveUnverifiedUploadCopyCommand(UploadId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
