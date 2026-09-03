using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.SearchRecords;

public class SearchUploadRecordsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndSearchesUploads()
    {
        var response = new UploadSearchResponse
        {
            Records = [],
            ContinuationToken = null
        };
        UploadServiceSearchRequest? capturedRequest = null;
        var uploadService = new Mock<IUploadService>();
        uploadService
            .Setup(x => x.SearchAsync(
                It.IsAny<UploadServiceSearchRequest>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<UploadServiceSearchRequest, Guid?, CancellationToken>((request, _, _) =>
                capturedRequest = request)
            .ReturnsAsync(response);
        var validator = new Mock<ICoreRequestValidator>();
        var handler = new SearchUploadRecordsQueryHandler(uploadService.Object, validator.Object);
        var query = new SearchUploadRecordsQuery(
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            "user-id",
            "document.txt",
            "abc123def456",
            4096L,
            null,
            null,
            null,
            null,
            null);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Same(response, result);
        Assert.Equal("document.txt", capturedRequest!.FileName);
        Assert.Equal("user-id", capturedRequest.UploadedByUserId);
        Assert.Equal("abc123def456", capturedRequest.Sha256Checksum);
        Assert.Equal(4096, capturedRequest.FileSizeBytes);
    }
}