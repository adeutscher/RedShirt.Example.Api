using RedShirt.Example.Api.Scaffolding.Editing;

namespace RedShirt.Example.Api.Scaffolding.UnitTests.Tests.Editing;

public sealed class TextFileTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void Write_KeepsBomAndCrLf()
    {
        byte[] original = [0xEF, 0xBB, 0xBF, (byte) 'a', (byte) '\r', (byte) '\n', (byte) 'b'];
        File.WriteAllBytes(_path, original);

        var file = TextFile.Read(_path);
        Assert.Equal("a\nb", file.Text);

        file.Text += "\nc";
        file.Write(_path);

        Assert.Equal([.. original, (byte) '\r', (byte) '\n', (byte) 'c'], File.ReadAllBytes(_path));
    }

    [Fact]
    public void Matches_IgnoresLineEndings()
    {
        File.WriteAllText(_path, "a\r\nb\r\n");

        Assert.True(TextFile.Matches(_path, "a\nb\n"));
        Assert.False(TextFile.Matches(_path, "a\nc\n"));
    }
}
