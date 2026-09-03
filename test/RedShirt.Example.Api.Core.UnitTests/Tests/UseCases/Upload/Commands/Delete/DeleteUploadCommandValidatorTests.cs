using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.Delete;

public class DeleteUploadCommandValidatorTests
{
    private readonly DeleteUploadCommandValidator _validator = new();

    [Fact]
    public async Task Validate_Succeeds_ForNonEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new DeleteUploadCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Fails_ForEmptyId()
    {
        var result = await _validator.ValidateAsync(
            new DeleteUploadCommand(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
