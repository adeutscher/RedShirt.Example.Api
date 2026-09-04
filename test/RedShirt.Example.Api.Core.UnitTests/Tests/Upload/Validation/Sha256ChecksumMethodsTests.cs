using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.Upload.Validation;

public class Sha256ChecksumMethodsTests
{
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void IsValid_AcceptsHexStringsOfCorrectLength(string checksum)
    {
        Assert.True(Sha256ChecksumMethods.IsValid(checksum));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc123")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdefg")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void IsValid_RejectsInvalidChecksums(string? checksum)
    {
        Assert.False(Sha256ChecksumMethods.IsValid(checksum));
    }
}