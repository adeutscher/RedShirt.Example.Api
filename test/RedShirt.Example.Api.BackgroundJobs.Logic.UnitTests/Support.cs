using RedShirt.Example.Api.BackgroundJobs.Core.Models;
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
