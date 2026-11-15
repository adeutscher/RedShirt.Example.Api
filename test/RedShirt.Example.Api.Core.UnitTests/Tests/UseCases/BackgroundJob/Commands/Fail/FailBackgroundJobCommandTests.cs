using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Fail;

public class FailBackgroundJobCommandTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ValidatesCommand_AndMapsToServiceRequest(bool canRetry)
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Failed);
        BackgroundJobServiceFailRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.FailAsync(It.IsAny<BackgroundJobServiceFailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceFailRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new FailBackgroundJobCommand(model.Id, 2, canRetry, "boom");

        var result = await new FailBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(model.Id, captured!.Id);
        Assert.Equal(2, captured.Attempt);
        Assert.Equal(canRetry, captured.CanRetry);
        Assert.Equal("boom", captured.Detail);
    }

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(true, 1, false)]
    [InlineData(false, 0, false)]
    public async Task Validator_ChecksFields(bool emptyId, int attempt, bool expected)
    {
        var validation = await new FailBackgroundJobCommandValidator().ValidateAsync(
            new FailBackgroundJobCommand(emptyId ? Guid.Empty : Guid.NewGuid(), attempt, true, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }
}
