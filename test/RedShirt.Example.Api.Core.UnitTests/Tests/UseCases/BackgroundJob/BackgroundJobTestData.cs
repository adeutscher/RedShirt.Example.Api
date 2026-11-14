using RedShirt.Example.Api.BackgroundJobs.Submission.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.BackgroundJob;

internal static class BackgroundJobTestData
{
    internal static BackgroundJobModel CreateModel(BackgroundJobState state = BackgroundJobState.Pending,
        Guid? id = null)
    {
        var now = DateTime.UtcNow;
        return new BackgroundJobModel
        {
            Id = id ?? Guid.NewGuid(),
            Type = BackgroundJobType.ValidateUpload,
            State = state,
            ParametersJson = "{\"uploadId\":\"00000000-0000-0000-0000-000000000001\"}",
            Attempts = 1,
            DateCreatedUtc = now,
            DateUpdatedUtc = now,
            DueAtUtc = now
        };
    }
}
