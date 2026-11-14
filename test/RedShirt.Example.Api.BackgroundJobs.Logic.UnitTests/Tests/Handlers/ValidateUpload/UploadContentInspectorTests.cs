using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;
using System.Text;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.UnitTests.Tests.Handlers.ValidateUpload;

public class UploadContentInspectorTests
{
    private static Task<bool> InspectAsync(string content)
    {
        return UploadContentInspector.IsValidAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)),
            TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("I like potato soup.", true)]
    [InlineData("POTATO", true)]
    [InlineData("I like tomato soup.", false)]
    [InlineData("pota to", false)]
    [InlineData("", false)]
    public async Task IsValidAsync_LooksForTheRequiredWord(string content, bool expected)
    {
        Assert.Equal(expected, await InspectAsync(content));
    }

    [Theory]
    [InlineData(8189)]
    [InlineData(8190)]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(20000)]
    public async Task IsValidAsync_FindsWordSplitAcrossReadChunks(int paddingLength)
    {
        Assert.True(await InspectAsync(new string('x', paddingLength) + "potato" + new string('y', 100)));
    }

    [Fact]
    public async Task IsValidAsync_LeavesStreamOpen()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("potato"));

        await UploadContentInspector.IsValidAsync(stream, TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
    }
}
