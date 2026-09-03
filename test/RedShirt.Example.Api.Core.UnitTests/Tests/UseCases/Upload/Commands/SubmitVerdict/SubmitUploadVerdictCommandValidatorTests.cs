using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.SubmitVerdict;

public class SubmitUploadVerdictCommandValidatorTests
{
    private readonly SubmitUploadVerdictCommandValidator _validator = new();

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyUploadId()
    {
        var result = await _validator.ValidateAsync(
            new SubmitUploadVerdictCommand(Guid.NewGuid(), true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_ForEmptyUploadId()
    {
        var result = await _validator.ValidateAsync(
            new SubmitUploadVerdictCommand(Guid.Empty, true),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
