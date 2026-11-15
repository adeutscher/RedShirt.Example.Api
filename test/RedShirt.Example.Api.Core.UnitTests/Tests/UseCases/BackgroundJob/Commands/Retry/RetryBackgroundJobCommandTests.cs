using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Retry;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Retry;

public class RetryBackgroundJobCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndRetries()
    {
        var model = BackgroundJobTestData.CreateModel();
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.RetryAsync(model.Id, It.IsAny<CancellationToken>())).ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new RetryBackgroundJobCommand(model.Id);

        var result = await new RetryBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyId()
    {
        var validation = await new RetryBackgroundJobCommandValidator().ValidateAsync(
            new RetryBackgroundJobCommand(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}