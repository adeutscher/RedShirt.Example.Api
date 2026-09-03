using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;
using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.Create;

public class CreateUploadCommandValidatorTests
{
    private readonly CreateUploadCommandValidator _validator = new();

    private static CreateUploadCommand CreateCommand(string fileName)
    {
        return new CreateUploadCommand(
            fileName,
            "user-id",
            "user",
            "203.0.113.10",
            Stream.Null,
            1024,
            "idem-key");
    }

    [Fact]
    public async Task Validate_Succeeds_ForPosixCompliantFileName()
    {
        var result = await _validator.ValidateAsync(
            CreateCommand("document.txt"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("bad name.txt")]
    public async Task Validate_Fails_ForNonPosixCompliantFileName(string fileName)
    {
        var result = await _validator.ValidateAsync(
            CreateCommand(fileName),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == PosixFileName.InvalidMessage);
    }

    [Fact]
    public async Task Validate_Fails_WhenFileNameIsEmpty()
    {
        var result = await _validator.ValidateAsync(
            CreateCommand(""),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
