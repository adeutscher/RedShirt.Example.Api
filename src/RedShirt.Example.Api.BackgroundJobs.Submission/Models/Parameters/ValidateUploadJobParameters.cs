namespace RedShirt.Example.Api.BackgroundJobs.Submission.Models.Parameters;

public sealed class ValidateUploadJobParameters : IBackgroundJobParameters
{
    public required Guid UploadId { get; init; }
    public static BackgroundJobType JobType => BackgroundJobType.ValidateUpload;
}
