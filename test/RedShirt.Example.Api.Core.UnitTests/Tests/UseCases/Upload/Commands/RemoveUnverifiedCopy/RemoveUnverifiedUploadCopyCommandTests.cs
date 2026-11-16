using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.RemoveUnverifiedCopy;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.RemoveUnverifiedCopy;

public class RemoveUnverifiedUploadCopyCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndRemoves()
    {
        var uploadId = Guid.NewGuid();
        var uploadService = new Mock<IUploadService>();
        var validator = new Mock<ICoreRequestValidator>();
        var command = new RemoveUnverifiedUploadCopyCommand(uploadId);

        await new RemoveUnverifiedUploadCopyCommandHandler(uploadService.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        uploadService.Verify(x => x.RemoveUnverifiedCopyAsync(uploadId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyUploadId()
    {
        var validation = await new RemoveUnverifiedUploadCopyCommandValidator().ValidateAsync(
            new RemoveUnverifiedUploadCopyCommand(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}