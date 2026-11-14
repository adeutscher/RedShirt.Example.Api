using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;

public interface ISearchBackgroundJobRecordsQueryHandler
    : ICqrsHandler<SearchBackgroundJobRecordsQuery, BackgroundJobSearchResponse>;

internal sealed class SearchBackgroundJobRecordsQueryHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : ISearchBackgroundJobRecordsQueryHandler
{
    public async Task<BackgroundJobSearchResponse> Handle(SearchBackgroundJobRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await backgroundJobService.SearchAsync(new BackgroundJobServiceSearchRequest
        {
            PageSize = query.PageSize,
            Type = query.Type,
            State = query.State,
            CreatedBeforeUtc = query.CreatedBeforeUtc
        }, cancellationToken);
    }
}
