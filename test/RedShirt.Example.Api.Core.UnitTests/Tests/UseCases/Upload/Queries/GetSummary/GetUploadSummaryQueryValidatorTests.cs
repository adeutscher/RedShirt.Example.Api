using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetSummary;

public class GetUploadSummaryQueryValidatorTests
{
    private readonly GetUploadSummaryQueryValidator _validator = new();

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadSummaryQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_ForEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadSummaryQuery(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
