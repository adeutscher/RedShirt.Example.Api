using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetDownloadLink;

public class GetUploadDownloadLinkQueryValidatorTests
{
    private readonly GetUploadDownloadLinkQueryValidator _validator = new();

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadDownloadLinkQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_ForEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadDownloadLinkQuery(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
