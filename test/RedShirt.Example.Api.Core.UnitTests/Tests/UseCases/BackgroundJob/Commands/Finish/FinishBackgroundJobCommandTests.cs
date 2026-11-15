using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Finish;

public class FinishBackgroundJobCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndMapsToServiceRequest()
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Succeeded);
        BackgroundJobServiceFinishRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.FinishAsync(It.IsAny<BackgroundJobServiceFinishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceFinishRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new FinishBackgroundJobCommand(model.Id, 2, "done");

        var result = await new FinishBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(model.Id, captured!.Id);
        Assert.Equal(2, captured.Attempt);
        Assert.Equal("done", captured.Detail);
    }

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(true, 1, false)]
    [InlineData(false, 0, false)]
    public async Task Validator_ChecksFields(bool emptyId, int attempt, bool expected)
    {
        var validation = await new FinishBackgroundJobCommandValidator().ValidateAsync(
            new FinishBackgroundJobCommand(emptyId ? Guid.Empty : Guid.NewGuid(), attempt, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }
}
