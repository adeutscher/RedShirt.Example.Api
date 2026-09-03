using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.Delete;

public class DeleteUploadCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPurgeFalse_CallsDeleteAsync()
    {
        var uploadId = Guid.NewGuid();
        var summary = new UploadSummaryModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = UploadState.Deleted,
            FileName = "file.txt",
            IsValidated = false,
            IsRejected = false
        };
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.DeleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        var validator = new Mock<ICoreRequestValidator>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<DeleteUploadCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new DeleteUploadCommandHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(new DeleteUploadCommand(uploadId), TestContext.Current.CancellationToken);

        Assert.Same(summary, result);
        uploadService.Verify(x => x.DeleteAsync(uploadId, It.IsAny<CancellationToken>()), Times.Once);
        uploadService.Verify(x => x.PurgeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPurgeTrue_CallsPurgeAsync()
    {
        var uploadId = Guid.NewGuid();
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.PurgeAsync(uploadId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var validator = new Mock<ICoreRequestValidator>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<DeleteUploadCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new DeleteUploadCommandHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(
            new DeleteUploadCommand(uploadId, true),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        uploadService.Verify(x => x.PurgeAsync(uploadId, It.IsAny<CancellationToken>()), Times.Once);
        uploadService.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}