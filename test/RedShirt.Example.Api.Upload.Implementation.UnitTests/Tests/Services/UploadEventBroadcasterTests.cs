using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Implementation.Services;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Services;

public class UploadEventBroadcasterTests
{
    private static readonly Guid UploadId = Guid.NewGuid();
    private readonly Mock<IBackgroundJobSubmitter> _submitter = new();

    private static UploadSummaryModel Summary => new()
    {
        Id = UploadId,
        DateCreatedUtc = DateTime.UtcNow,
        DateUpdatedUtc = DateTime.UtcNow,
        UploadedByUserId = "user-id",
        State = UploadState.NotValidated,
        FileName = "file.txt",
        IsValidated = false,
        IsRejected = false
    };

    [Fact]
    public async Task BroadcastUploadCompletedAsync_SubmitsValidationJob()
    {
        ValidateUploadJobParameters? captured = null;
        _submitter.Setup(x => x.SubmitAsync(It.IsAny<ValidateUploadJobParameters>(), It.IsAny<CancellationToken>()))
            .Callback<ValidateUploadJobParameters, CancellationToken>((parameters, _) => captured = parameters)
            .ReturnsAsync(Guid.NewGuid());

        await new UploadEventBroadcaster(_submitter.Object).BroadcastUploadCompletedAsync(
            new UploadCompletedEvent
            {
                UploadId = UploadId,
                StorageObjectKey = "user-id/key",
                Sha256Checksum = "abc",
                FileSizeBytes = 10
            }, Summary, TestContext.Current.CancellationToken);

        Assert.Equal(UploadId, captured!.UploadId);
    }

    [Fact]
    public async Task BroadcastUploadValidatedAsync_SubmitsMoveJob()
    {
        MoveUploadJobParameters? captured = null;
        _submitter.Setup(x => x.SubmitAsync(It.IsAny<MoveUploadJobParameters>(), It.IsAny<CancellationToken>()))
            .Callback<MoveUploadJobParameters, CancellationToken>((parameters, _) => captured = parameters)
            .ReturnsAsync(Guid.NewGuid());

        await new UploadEventBroadcaster(_submitter.Object).BroadcastUploadValidatedAsync(
            new UploadValidatedEvent {UploadId = UploadId}, Summary, TestContext.Current.CancellationToken);

        Assert.Equal(UploadId, captured!.UploadId);
    }

    [Fact]
    public async Task OtherEvents_SubmitNoJobs()
    {
        var broadcaster = new UploadEventBroadcaster(_submitter.Object);
        var token = TestContext.Current.CancellationToken;

        await broadcaster.BroadcastUploadCreatedAsync(new UploadCreatedEvent
        {
            UploadId = UploadId,
            UploadedByUserId = "user-id",
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = "file.txt",
            IdempotencyKey = "key"
        }, Summary, token);
        await broadcaster.BroadcastUploadRejectedAsync(new UploadRejectedEvent {UploadId = UploadId}, Summary, token);
        await broadcaster.BroadcastUploadStoredAsync(
            new UploadStoredEvent {UploadId = UploadId, VerifiedStorageObjectKey = "key"}, Summary, token);
        await broadcaster.BroadcastUploadDeletedAsync(new UploadDeletedEvent {UploadId = UploadId}, Summary, token);
        await broadcaster.BroadcastUploadPurgedAsync(new UploadPurgedEvent {UploadId = UploadId}, token);

        Assert.Empty(_submitter.Invocations);
    }
}