using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Implementation.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Implementation.UnitTests.Tests.Services;

public class BackgroundJobSubmitterTests
{
    [Fact]
    public async Task SubmitAsync_CreatesJobOfParametersType_WithSerializedParameters_AndWakesRunner()
    {
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        BackgroundJobServiceCreateRequest? captured = null;
        var service = new Mock<IBackgroundJobService>();
        service
            .Setup(x => x.CreateAsync(It.IsAny<BackgroundJobServiceCreateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundJobServiceCreateRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new BackgroundJobModel
            {
                Id = jobId,
                Type = BackgroundJobType.ValidateUpload,
                State = BackgroundJobState.Pending,
                ParametersJson = "{}",
                Attempts = 0,
                DateCreatedUtc = DateTime.UtcNow,
                DateUpdatedUtc = DateTime.UtcNow,
                DueAtUtc = DateTime.UtcNow
            });
        var wakeSignal = new Mock<IBackgroundJobWakeSignal>();
        var submitter = new BackgroundJobSubmitter(service.Object, wakeSignal.Object);

        var result = await submitter.SubmitAsync(new ValidateUploadJobParameters {UploadId = uploadId},
            TestContext.Current.CancellationToken);

        Assert.Equal(jobId, result);
        Assert.Equal(BackgroundJobType.ValidateUpload, captured!.Type);
        var parameters = JsonSerializer.Deserialize<ValidateUploadJobParameters>(captured.ParametersJson,
            BackgroundJobJson.Options);
        Assert.Equal(uploadId, parameters!.UploadId);
        wakeSignal.Verify(x => x.Wake(), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenCreateFails_DoesNotWakeRunner()
    {
        var service = new Mock<IBackgroundJobService>();
        service
            .Setup(x => x.CreateAsync(It.IsAny<BackgroundJobServiceCreateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var wakeSignal = new Mock<IBackgroundJobWakeSignal>();
        var submitter = new BackgroundJobSubmitter(service.Object, wakeSignal.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => submitter.SubmitAsync(
            new ValidateUploadJobParameters {UploadId = Guid.NewGuid()}, TestContext.Current.CancellationToken));

        wakeSignal.Verify(x => x.Wake(), Times.Never);
    }
}