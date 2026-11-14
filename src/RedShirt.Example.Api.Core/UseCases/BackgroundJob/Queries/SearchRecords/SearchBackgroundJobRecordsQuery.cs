using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;

public sealed record SearchBackgroundJobRecordsQuery(
    int PageSize,
    BackgroundJobType? Type,
    BackgroundJobState? State,
    DateTime? CreatedBeforeUtc);
