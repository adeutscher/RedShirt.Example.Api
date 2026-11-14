using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Services;

public class CqrsBackgroundJobSourceTests
{
    private readonly Mock<IAcknowledgeBackgroundJobCommandHandler> _acknowledgeHandler = new();
    private readonly Mock<ILeaseBackgroundJobsCommandHandler> _leaseHandler = new();

    private CqrsBackgroundJobSource CreateSource()
    {
        return new CqrsBackgroundJobSource(_leaseHandler.Object, _acknowledgeHandler.Object,
            Options.Create(new BackgroundJobRunnerOptions {ProcessName = "api-1"}));
    }

    [Fact]
    public async Task GetJobsAsync_LeasesAsThisProcess_AndConvertsToJobModels()
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Running);
        _leaseHandler.Setup(x => x.Handle(new LeaseBackgroundJobsCommand(4, "api-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([model]);

        var jobs = await CreateSource().GetJobsAsync(4, TestContext.Current.CancellationToken);

        var job = Assert.IsType<JobModel>(Assert.Single(jobs));
        Assert.Equal(model.Id.ToString(), job.MessageId);
        Assert.Equal(model.DateCreatedUtc, job.CreatedAtUtc);
        Assert.Equal(model.Attempts, job.Attempts);
        Assert.Equal(model.Type, job.Data.Type);
        Assert.Equal(model.ParametersJson, job.Data.ParametersJson);
    }

    [Fact]
    public async Task AcknowledgeAsync_ReportsResultDetailAndLeasedAttemptAsThisProcess()
    {
        var model = BackgroundJobTestData.CreateModel(BackgroundJobState.Running);
        var job = CqrsBackgroundJobSource.ToJobModel(model);

        await CreateSource().AcknowledgeAsync(job,
            new JobLogicRunnerResponse {Result = JobResult.InvalidData, Detail = "bad"},
            TestContext.Current.CancellationToken);

        _acknowledgeHandler.Verify(x => x.Handle(
            new AcknowledgeBackgroundJobCommand(model.Id, "api-1", model.Attempts,
                BackgroundJobAttemptResult.InvalidData, "bad"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcknowledgeAsync_ForJobNotLeasedBySource_Throws()
    {
        var job = new Mock<IJobModel>();
        job.SetupGet(x => x.MessageId).Returns(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentException>(() => CreateSource().AcknowledgeAsync(job.Object,
            JobLogicRunnerResponse.Success, TestContext.Current.CancellationToken));
        _acknowledgeHandler.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(JobResult.Success, BackgroundJobAttemptResult.Success)]
    [InlineData(JobResult.Failure, BackgroundJobAttemptResult.Failure)]
    [InlineData(JobResult.InvalidData, BackgroundJobAttemptResult.InvalidData)]
    public void ToAttemptResult_MapsEveryJobResult(JobResult result, BackgroundJobAttemptResult expected)
    {
        Assert.Equal(expected, CqrsBackgroundJobSource.ToAttemptResult(result));
    }

    [Fact]
    public void ToAttemptResult_RejectsUnknownJobResult()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CqrsBackgroundJobSource.ToAttemptResult((JobResult) 99));
    }
}
