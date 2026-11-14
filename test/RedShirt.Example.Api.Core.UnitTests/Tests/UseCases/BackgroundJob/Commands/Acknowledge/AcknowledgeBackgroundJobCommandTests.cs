using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Acknowledge;

public class AcknowledgeBackgroundJobCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndMapsToServiceRequest()
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Succeeded);
        BackgroundJobServiceAcknowledgeRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.AcknowledgeAsync(It.IsAny<BackgroundJobServiceAcknowledgeRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceAcknowledgeRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(model);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new AcknowledgeBackgroundJobCommand(model.Id, "owner", 2, BackgroundJobAttemptResult.Success, "done");

        var result = await new AcknowledgeBackgroundJobCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(model, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(model.Id, captured!.Id);
        Assert.Equal("owner", captured.LeaseOwner);
        Assert.Equal(2, captured.Attempt);
        Assert.Equal(BackgroundJobAttemptResult.Success, captured.Result);
        Assert.Equal("done", captured.Detail);
    }

    [Theory]
    [InlineData(false, "owner", 1, BackgroundJobAttemptResult.Success, true)]
    [InlineData(true, "owner", 1, BackgroundJobAttemptResult.Success, false)]
    [InlineData(false, "", 1, BackgroundJobAttemptResult.Success, false)]
    [InlineData(false, "owner", 0, BackgroundJobAttemptResult.Success, false)]
    [InlineData(false, "owner", 1, (BackgroundJobAttemptResult) 99, false)]
    public async Task Validator_ChecksFields(bool emptyId, string leaseOwner, int attempt, BackgroundJobAttemptResult result,
        bool expected)
    {
        var validation = await new AcknowledgeBackgroundJobCommandValidator().ValidateAsync(
            new AcknowledgeBackgroundJobCommand(emptyId ? Guid.Empty : Guid.NewGuid(), leaseOwner, attempt, result,
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsLongLeaseOwner()
    {
        var validation = await new AcknowledgeBackgroundJobCommandValidator().ValidateAsync(
            new AcknowledgeBackgroundJobCommand(Guid.NewGuid(), new string('o', 129), 1, BackgroundJobAttemptResult.Success, null),
            TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}
