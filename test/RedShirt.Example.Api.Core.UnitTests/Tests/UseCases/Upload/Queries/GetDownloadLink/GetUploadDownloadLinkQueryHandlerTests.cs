using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetDownloadLink;

public class GetUploadDownloadLinkQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndReturnsDownloadLink()
    {
        var uploadId = Guid.NewGuid();
        var link = new UploadDownloadLinkModel
        {
            DownloadUrl = "https://example.test/download",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.GetDownloadLinkAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new GetUploadDownloadLinkQueryHandler(uploadService.Object, validator.Object);

        var result = await handler.Handle(
            new GetUploadDownloadLinkQuery(uploadId),
            TestContext.Current.CancellationToken);

        Assert.Same(link, result);
    }
}