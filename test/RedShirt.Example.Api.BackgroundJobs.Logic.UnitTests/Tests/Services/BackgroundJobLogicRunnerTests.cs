using Microsoft.Extensions.Logging.Abstractions;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Services;

public class BackgroundJobLogicRunnerTests
{
    [Fact]
    public async Task RunAsync_DispatchesToHandlerForType()
    {
        var job = JobFactory.Create(BackgroundJobType.ValidateUpload, "{}");
        var handler = new Mock<IBackgroundJobHandler>();
        handler.SetupGet(x => x.Type).Returns(BackgroundJobType.ValidateUpload);
        handler.Setup(x => x.HandleAsync(job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobLogicRunnerResponse.Success);
        var runner = new BackgroundJobLogicRunner([handler.Object], NullLogger<BackgroundJobLogicRunner>.Instance);

        var response = await runner.RunAsync(job, TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.Success, response.Result);
        handler.Verify(x => x.HandleAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithoutHandlerForType_ReturnsInvalidData()
    {
        var runner = new BackgroundJobLogicRunner([], NullLogger<BackgroundJobLogicRunner>.Instance);

        var response = await runner.RunAsync(JobFactory.Create(BackgroundJobType.ValidateUpload, "{}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.InvalidData, response.Result);
        Assert.Contains(nameof(BackgroundJobType.ValidateUpload), ((JobLogicRunnerResponse) response).Detail);
    }
}