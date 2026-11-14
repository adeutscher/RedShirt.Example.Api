using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.Services;

/// <summary>
///     Submits jobs to the BackgroundJob table. To hand jobs to an applied JobWorker instead, register an
///     <see cref="IBackgroundJobSubmitter" /> that publishes to that worker's queue.
/// </summary>
internal sealed class TableBackgroundJobSubmitter(IBackgroundJobService backgroundJobService)
    : IBackgroundJobSubmitter
{
    public async Task<Guid> SubmitAsync<TParameters>(TParameters parameters,
        CancellationToken cancellationToken = default) where TParameters : IBackgroundJobParameters
    {
        var model = await backgroundJobService.CreateAsync(new BackgroundJobServiceCreateRequest
        {
            Type = TParameters.JobType,
            ParametersJson = JsonSerializer.Serialize(parameters, BackgroundJobJson.Options)
        }, cancellationToken);

        return model.Id;
    }
}
