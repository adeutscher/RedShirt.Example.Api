using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.CopyToVerifiedStorage;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.CopyToVerifiedStorage;

public class CopyUploadToVerifiedStorageCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndCopies()
    {
        var uploadId = Guid.NewGuid();
        var uploadService = new Mock<IUploadService>();
        uploadService.Setup(x => x.CopyToVerifiedStorageAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("key");
        var validator = new Mock<ICoreRequestValidator>();
        var command = new CopyUploadToVerifiedStorageCommand(uploadId);

        var result = await new CopyUploadToVerifiedStorageCommandHandler(uploadService.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal("key", result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyUploadId()
    {
        var validation = await new CopyUploadToVerifiedStorageCommandValidator().ValidateAsync(
            new CopyUploadToVerifiedStorageCommand(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}
