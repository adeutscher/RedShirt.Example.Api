using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.GetRecord;

public interface IGetBackgroundJobRecordQueryHandler : ICqrsHandler<GetBackgroundJobRecordQuery, BackgroundJobModel>;

internal sealed class GetBackgroundJobRecordQueryHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IGetBackgroundJobRecordQueryHandler
{
    public async Task<BackgroundJobModel> Handle(GetBackgroundJobRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await backgroundJobService.GetAsync(query.Id, cancellationToken);
    }
}
