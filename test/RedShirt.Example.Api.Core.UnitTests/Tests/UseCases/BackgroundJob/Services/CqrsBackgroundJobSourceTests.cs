using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob.Services;

public class CqrsBackgroundJobSourceTests
{
    private readonly Mock<ILeaseBackgroundJobsCommandHandler> _leaseHandler = new();

    private CqrsBackgroundJobSource CreateSource()
    {
        return new CqrsBackgroundJobSource(_leaseHandler.Object,
            Options.Create(new BackgroundJobRunnerOptions {ProcessName = "api-1"}));
    }

    [Fact]
    public async Task GetJobsAsync_LeasesAsThisProcess_AndConvertsToJobModels()
    {
        var model = BackgroundJobTestData.CreateModel();
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
}