namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;

/// <summary>
///     What the upload validation job needs from the upload system. Inside the API, this is implemented with the upload
///     CQRS handlers. In an applied JobWorker, it would be implemented with a connector to the API's upload endpoints
///     (GET <c>/uploads/{id}</c>, GET <c>/uploads/{id}/download-link</c>, POST <c>/uploads/{id}/verdicts</c>).
/// </summary>
public interface IUploadValidationService
{
    /// <summary>
    ///     False when the upload no longer exists or is no longer waiting for a verdict (for example, a repeated job).
    /// </summary>
    Task<bool> IsAwaitingValidationAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Opens the uploaded file's content for reading. The caller disposes the stream.
    /// </summary>
    Task<Stream> OpenContentAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task SubmitVerdictAsync(Guid uploadId, bool approved, CancellationToken cancellationToken = default);
}
