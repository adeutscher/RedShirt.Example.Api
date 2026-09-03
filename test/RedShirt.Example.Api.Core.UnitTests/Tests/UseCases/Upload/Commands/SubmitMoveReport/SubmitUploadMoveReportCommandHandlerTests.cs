using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.SubmitMoveReport;

public class SubmitUploadMoveReportCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndSubmitsMoveReport()
    {
        var uploadId = Guid.NewGuid();
        const string verifiedKey = "verified/user-id/upload-id";
        var summary = new UploadSummaryModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = UploadState.Stored,
            FileName = "document.txt",
            IsValidated = true,
            IsRejected = false
        };
        UploadServiceMoveReportRequest? capturedRequest = null;
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.SubmitMoveReportAsync(It.IsAny<UploadServiceMoveReportRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadServiceMoveReportRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(summary);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new SubmitUploadMoveReportCommandHandler(uploadService.Object, validator.Object);
        var command = new SubmitUploadMoveReportCommand(uploadId, verifiedKey);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(summary, result);
        Assert.Equal(uploadId, capturedRequest!.UploadId);
        Assert.Equal(verifiedKey, capturedRequest.VerifiedStorageObjectKey);
    }
}
