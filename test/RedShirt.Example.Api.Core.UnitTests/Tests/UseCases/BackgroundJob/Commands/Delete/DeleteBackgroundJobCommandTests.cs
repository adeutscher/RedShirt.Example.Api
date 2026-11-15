using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Delete;

public class DeleteBackgroundJobCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndDeletes()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IBackgroundJobService>();
        var validator = new Mock<ICoreRequestValidator>();
        var command = new DeleteBackgroundJobCommand(id);

        await new DeleteBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyId()
    {
        var validation = await new DeleteBackgroundJobCommandValidator().ValidateAsync(
            new DeleteBackgroundJobCommand(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}