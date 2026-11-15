namespace RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;

/// <summary>
///     A background job's parameters model. Each job type has one, and the type is declared on the model, so a submitter
///     only needs to pass the model.
/// </summary>
public interface IBackgroundJobParameters
{
    static abstract BackgroundJobType JobType { get; }
}