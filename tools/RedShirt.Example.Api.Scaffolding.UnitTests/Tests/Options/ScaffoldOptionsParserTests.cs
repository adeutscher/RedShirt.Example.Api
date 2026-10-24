using RedShirt.Example.Api.Scaffolding.Options;

namespace RedShirt.Example.Api.Scaffolding.UnitTests.Tests.Options;

public class ScaffoldOptionsParserTests
{
    [Fact]
    public void Parse_ReadsCreateOptions()
    {
        var options = ScaffoldOptionsParser.Parse(
            ["create", "BeansWidget", "--scope", "customer", "--route", "widgets", "--manifest", "out.txt", "--dry-run"]);

        Assert.Equal(ScaffoldMode.Create, options.Mode);
        Assert.Equal("BeansWidget", options.Resource);
        Assert.True(options.CustomerScope);
        Assert.Equal("widgets", options.Route);
        Assert.Equal("out.txt", options.ManifestPath);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_ReadsRemoveFlags()
    {
        var options = ScaffoldOptionsParser.Parse(["remove", "BeansWidget", "-y", "--force"]);

        Assert.Equal(ScaffoldMode.Remove, options.Mode);
        Assert.True(options.AssumeYes);
        Assert.True(options.Force);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update", "BeansWidget")]
    [InlineData("create", "beansWidget")]
    [InlineData("create", "BeansWidget", "--scope", "tenant")]
    [InlineData("create", "BeansWidget", "--route")]
    [InlineData("create", "BeansWidget", "-y")]
    [InlineData("create", "BeansWidget", "--unknown")]
    public void Parse_Rejects_InvalidArguments(params string[] args)
    {
        Assert.Throws<OptionsException>(() => ScaffoldOptionsParser.Parse(args));
    }
}
