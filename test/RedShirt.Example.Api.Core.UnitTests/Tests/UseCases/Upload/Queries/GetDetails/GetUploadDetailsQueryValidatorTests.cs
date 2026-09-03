using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetDetails;

public class GetUploadDetailsQueryValidatorTests
{
    private readonly GetUploadDetailsQueryValidator _validator = new();

    [Fact]
    public async Task Validate_Fails_ForEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadDetailsQuery(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadDetailsQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}