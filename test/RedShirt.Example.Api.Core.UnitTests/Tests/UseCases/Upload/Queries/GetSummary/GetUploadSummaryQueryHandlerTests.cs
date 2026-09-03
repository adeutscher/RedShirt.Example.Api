using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetSummary;

public class GetUploadSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndReturnsSummary()
    {
        var uploadId = Guid.NewGuid();
        var summary = new UploadSummaryModel
        {
            Id = uploadId,
            DateCreatedUtc = DateTime.UtcNow,
            DateUpdatedUtc = DateTime.UtcNow,
            UploadedByUserId = "user-id",
            State = UploadState.NotValidated,
            FileName = "document.txt",
            IsValidated = false,
            IsRejected = false
        };
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.GetSummaryAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new GetUploadSummaryQueryHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(
            new GetUploadSummaryQuery(uploadId),
            TestContext.Current.CancellationToken);

        Assert.Same(summary, result);
    }
}