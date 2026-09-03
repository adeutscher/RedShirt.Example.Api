using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetInternalDetails;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.GetInternalDetails;

public class GetUploadInternalDetailsQueryValidatorTests
{
    private readonly GetUploadInternalDetailsQueryValidator _validator = new();

    [Fact]
    public async Task Validate_Fails_ForEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadInternalDetailsQuery(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new GetUploadInternalDetailsQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}