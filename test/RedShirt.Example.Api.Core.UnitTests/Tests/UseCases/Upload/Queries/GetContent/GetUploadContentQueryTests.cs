using Moq;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetContent;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetContent;

public class GetUploadContentQueryTests
{
    [Fact]
    public async Task Handle_ValidatesQuery_AndOpensContent()
    {
        var uploadId = Guid.NewGuid();
        await using var content = new MemoryStream();
        var uploadService = new Mock<IUploadService>();
        uploadService.Setup(x => x.OpenContentAsync(uploadId, It.IsAny<CancellationToken>())).ReturnsAsync(content);
        var validator = new Mock<ICoreRequestValidator>();
        var query = new GetUploadContentQuery(uploadId);

        var result = await new GetUploadContentQueryHandler(uploadService.Object, validator.Object)
            .Handle(query, TestContext.Current.CancellationToken);

        Assert.Same(content, result);
        validator.Verify(x => x.ValidateAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validator_Fails_ForEmptyId()
    {
        var validation = await new GetUploadContentQueryValidator().ValidateAsync(
            new GetUploadContentQuery(Guid.Empty), TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
    }
}
