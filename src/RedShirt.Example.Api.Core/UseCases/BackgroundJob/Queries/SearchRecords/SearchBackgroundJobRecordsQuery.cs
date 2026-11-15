using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;

public sealed record SearchBackgroundJobRecordsQuery(
    int PageSize,
    BackgroundJobType? Type,
    BackgroundJobState? State,
    DateTime? CreatedBeforeUtc,
    DateTime? CompletedBeforeUtc = null);