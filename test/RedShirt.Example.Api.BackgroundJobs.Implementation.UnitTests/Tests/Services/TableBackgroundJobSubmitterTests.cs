using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.UnitTests.Tests.Services;

public class TableBackgroundJobSubmitterTests
{
    [Fact]
    public async Task SubmitAsync_CreatesJobOfParametersType_WithSerializedParameters()
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
        var submitter = new TableBackgroundJobSubmitter(service.Object);

        var result = await submitter.SubmitAsync(new ValidateUploadJobParameters {UploadId = uploadId},
            TestContext.Current.CancellationToken);

        Assert.Equal(jobId, result);
        Assert.Equal(BackgroundJobType.ValidateUpload, captured!.Type);
        var parameters = JsonSerializer.Deserialize<ValidateUploadJobParameters>(captured.ParametersJson,
            BackgroundJobJson.Options);
        Assert.Equal(uploadId, parameters!.UploadId);
    }
}
