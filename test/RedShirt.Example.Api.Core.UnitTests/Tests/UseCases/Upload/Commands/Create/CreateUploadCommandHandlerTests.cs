using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.Create;

public class CreateUploadCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndCreatesUpload()
    {
        var uploadId = Guid.NewGuid();
        var summary = new UploadSummaryModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = UploadState.Uploading,
            FileName = "document.txt",
            IsValidated = false,
            IsRejected = false
        };
        UploadServiceCreateRequest? capturedRequest = null;
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.CreateAsync(It.IsAny<UploadServiceCreateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadServiceCreateRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(summary);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new CreateUploadCommandHandler(uploadService.Object, validator.Object);
        var command = new CreateUploadCommand(
            "document.txt",
            "user-id",
            "user",
            "203.0.113.10",
            Stream.Null,
            1024,
            "idem-key");

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(summary, result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("document.txt", capturedRequest!.FileName);
        validator.Verify(x => x.ValidateAsync(command, TestContext.Current.CancellationToken), Times.Once);
        uploadService.Verify(
            x => x.CreateAsync(It.IsAny<UploadServiceCreateRequest>(), TestContext.Current.CancellationToken),
            Times.Once);
    }
}