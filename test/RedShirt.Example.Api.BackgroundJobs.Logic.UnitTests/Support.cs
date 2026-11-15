using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests;

internal static class JobFactory
{
    internal static JobModel Create(BackgroundJobType type, string parametersJson)
    {
        return new JobModel
        {
            MessageId = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 1,
            Data = new JobDataModel
            {
                Type = type,
                ParametersJson = parametersJson
            }
        };
    }
}

internal static class StatusServiceFactory
{
    /// <summary>
    ///     A status service that accepts every report.
    /// </summary>
    internal static Mock<IBackgroundJobStatusService> CreateAccepting()
    {
        var service = new Mock<IBackgroundJobStatusService>();
        service.Setup(x => x.ReportStartedAsync(It.IsAny<IJobModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        service.Setup(x => x.ReportFinishedAsync(It.IsAny<IJobModel>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        service.Setup(x => x.ReportFailedAsync(It.IsAny<IJobModel>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return service;
    }
}