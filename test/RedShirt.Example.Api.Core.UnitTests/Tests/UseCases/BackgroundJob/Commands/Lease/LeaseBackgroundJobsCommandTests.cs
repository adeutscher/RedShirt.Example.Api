using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Commands.Lease;

public class LeaseBackgroundJobsCommandTests
{
    [Fact]
    public async Task Handle_ValidatesCommand_AndMapsToServiceRequest()
    {
        IReadOnlyList<BackgroundJobModel> models = [BackgroundJobTestData.CreateModel(BackgroundJobState.Running)];
        BackgroundJobServiceLeaseRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service.Setup(x => x.LeaseAsync(It.IsAny<BackgroundJobServiceLeaseRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceLeaseRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(models);
        var validator = new Mock<ICoreRequestValidator>();
        var command = new LeaseBackgroundJobsCommand(3, "owner");

        var result = await new LeaseBackgroundJobsCommandHandler(service.Object, validator.Object)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Same(models, result);
        validator.Verify(x => x.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(3, captured!.Count);
        Assert.Equal("owner", captured.LeaseOwner);
    }

    [Theory]
    [InlineData(1, "owner", true)]
    [InlineData(100, "owner", true)]
    [InlineData(0, "owner", false)]
    [InlineData(101, "owner", false)]
    [InlineData(5, "", false)]
    public async Task Validator_ChecksFields(int count, string leaseOwner, bool expected)
    {
        var validation = await new LeaseBackgroundJobsCommandValidator().ValidateAsync(
            new LeaseBackgroundJobsCommand(count, leaseOwner), TestContext.Current.CancellationToken);

        Assert.Equal(expected, validation.IsValid);
    }
}
