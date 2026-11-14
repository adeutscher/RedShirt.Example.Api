namespace RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;

public sealed class MoveUploadJobParameters : IBackgroundJobParameters
{
    public required Guid UploadId { get; init; }
    public static BackgroundJobType JobType => BackgroundJobType.MoveUpload;
}
