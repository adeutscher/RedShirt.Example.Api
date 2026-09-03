using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.Upload.Validation;

public class PosixFileNameTests
{
    [Theory]
    [InlineData("document.txt")]
    [InlineData("My_File-2024.pdf")]
    [InlineData("a")]
    [InlineData("file.name.with.dots")]
    public void IsValid_AcceptsPortableFileNames(string fileName)
    {
        Assert.True(PosixFileName.IsValid(fileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("path/file.txt")]
    [InlineData("path\\file.txt")]
    [InlineData("file name.txt")]
    [InlineData("file:name.txt")]
    public void IsValid_RejectsNonPortableFileNames(string? fileName)
    {
        Assert.False(PosixFileName.IsValid(fileName));
    }

    [Fact]
    public void IsValid_RejectsNamesLongerThanMaxLength()
    {
        var fileName = new string('a', PosixFileName.MaxLength + 1);

        Assert.False(PosixFileName.IsValid(fileName));
    }
}
