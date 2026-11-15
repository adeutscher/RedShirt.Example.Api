using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Start;

public class StartBackgroundJobCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndMapsToServiceRequest()
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Running);
        BackgroundJobServiceStartRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.StartAsync(It.IsAny<BackgroundJobServiceStartRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceStartRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new StartBackgroundJobCommand(model.Id, 2);

        var result = await new StartBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(model.Id, captured!.Id);
        Assert.Equal(2, captured.Attempt);
    }

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(true, 1, false)]
    [InlineData(false, 0, false)]
    public async Task Validator_ChecksFields(bool emptyId, int attempt, bool expected)
    {
        var validation = await new StartBackgroundJobCommandValidator().ValidateAsync(
            new StartBackgroundJobCommand(emptyId ? Guid.Empty : Guid.NewGuid(), attempt),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }
}
