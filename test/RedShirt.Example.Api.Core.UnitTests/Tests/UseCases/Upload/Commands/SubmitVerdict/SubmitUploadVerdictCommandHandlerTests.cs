using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.SubmitVerdict;

public class SubmitUploadVerdictCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndSubmitsVerdict()
    {
        var uploadId = Guid.NewGuid();
        var summary = new UploadSummaryModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = UploadState.Verified,
            FileName = "document.txt",
            IsValidated = true,
            IsRejected = false
        };
        UploadServiceVerdictRequest? capturedRequest = null;
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.SubmitVerdictAsync(It.IsAny<UploadServiceVerdictRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadServiceVerdictRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(summary);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new SubmitUploadVerdictCommandHandler(uploadService.Object, validator.Object);
        var command = new SubmitUploadVerdictCommand(uploadId, true);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(summary, result);
        Assert.Equal(uploadId, capturedRequest!.UploadId);
        Assert.True(capturedRequest.Approved);
    }
}