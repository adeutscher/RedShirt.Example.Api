namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;

/// <summary>
///     Where an upload is in its move from the unverified bucket to the verified bucket.
/// </summary>
public enum UploadMoveStage
{
    /// <summary>
    ///     Verified, and not yet reported as moved.
    /// </summary>
    AwaitingMove,

    /// <summary>
    ///     Already reported as moved. Only cleanup of the unverified copy can be left.
    /// </summary>
    Stored,

    /// <summary>
    ///     Gone, or in a state that this job does not act on.
    /// </summary>
    NotApplicable
}

/// <summary>
///     What the upload move job needs from the upload system. Inside the API, this is implemented with the upload CQRS
///     handlers. In an applied JobWorker, the stage and the move report would go through a connector to the API's upload
///     endpoints (GET <c>/uploads/{id}</c>, GET <c>/uploads/{id}/details/internal</c>,
///     POST <c>/uploads/{id}/move-reports</c>), and the copy and delete would use storage APIs directly.
/// </summary>
public interface IUploadStorageService
{
    /// <summary>
    ///     Copies the upload's file to the verified bucket. Safe to repeat.
    /// </summary>
    /// <returns>The object key in the verified bucket.</returns>
    Task<string> CopyToVerifiedStorageAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task<UploadMoveStage> GetMoveStageAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the unverified copy of a stored upload. Safe to repeat.
    /// </summary>
    Task RemoveUnverifiedCopyAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task SubmitMoveReportAsync(Guid uploadId, string verifiedStorageObjectKey,
        CancellationToken cancellationToken = default);
}