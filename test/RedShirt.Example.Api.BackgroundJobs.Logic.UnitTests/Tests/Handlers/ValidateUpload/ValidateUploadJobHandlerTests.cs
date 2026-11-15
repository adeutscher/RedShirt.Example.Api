using Microsoft.Extensions.Logging.Abstractions;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using System.Text;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Handlers.ValidateUpload;

public class ValidateUploadJobHandlerTests
{
    private static readonly Guid UploadId = Guid.NewGuid();

    private static JobModel CreateJob(string? parametersJson = null)
    {
        return JobFactory.Create(BackgroundJobType.ValidateUpload,
            parametersJson ?? $"{{\"uploadId\":\"{UploadId}\"}}");
    }

    private readonly Mock<IBackgroundJobStatusService> _statusService = StatusServiceFactory.CreateAccepting();

    private ValidateUploadJobHandler CreateHandler(Mock<IUploadValidationService> service)
    {
        return new ValidateUploadJobHandler(service.Object, _statusService.Object,
            NullLogger<ValidateUploadJobHandler>.Instance);
    }

    private static Mock<IUploadValidationService> CreateService(bool awaiting, string content = "")
    {
        var service = new Mock<IUploadValidationService>();
        service.Setup(x => x.IsAwaitingValidationAsync(UploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(awaiting);
        service.Setup(x => x.OpenContentAsync(UploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
        return service;
    }

    [Theory]
    [InlineData("A potato document.", true)]
    [InlineData("Not that kind of document.", false)]
    public async Task HandleAsync_SubmitsVerdictFromContent(string content, bool expectedApproval)
    {
        var service = CreateService(true, content);

        var response = await CreateHandler(service).HandleAsync(CreateJob(), TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.Success, response.Result);
        service.Verify(x => x.SubmitVerdictAsync(UploadId, expectedApproval, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAwaitingValidation_SucceedsWithoutVerdict()
    {
        var service = CreateService(false);

        var response = await CreateHandler(service).HandleAsync(CreateJob(), TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.Success, response.Result);
        service.Verify(x => x.OpenContentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(
            x => x.SubmitVerdictAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_ReportsRetryableFailure()
    {
        var service = CreateService(true);
        service.Setup(x => x.OpenContentAsync(UploadId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));

        var response = await CreateHandler(service).HandleAsync(CreateJob(), TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.Failure, response.Result);
        _statusService.Verify(x => x.ReportFailedAsync(It.IsAny<IJobModel>(), true,
            It.Is<string?>(detail => detail!.Contains("storage unavailable")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"uploadId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{}")]
    public async Task HandleAsync_WithUnusableParameters_ReturnsInvalidData(string parametersJson)
    {
        var service = CreateService(true, "potato");

        var response = await CreateHandler(service).HandleAsync(CreateJob(parametersJson),
            TestContext.Current.CancellationToken);

        Assert.Equal(JobResult.InvalidData, response.Result);
        service.Verify(
            x => x.SubmitVerdictAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Type_IsValidateUpload()
    {
        Assert.Equal(BackgroundJobType.ValidateUpload, CreateHandler(CreateService(false)).Type);
    }
}