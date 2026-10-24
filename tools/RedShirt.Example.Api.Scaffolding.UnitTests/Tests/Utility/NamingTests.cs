using RedShirt.Example.Api.Scaffolding.Utility;

namespace RedShirt.Example.Api.Scaffolding.UnitTests.Tests.Utility;

public class NamingTests
{
    [Theory]
    [InlineData("Order", "order")]
    [InlineData("BeansWidget", "beans-widget")]
    [InlineData("HTTPRequest", "http-request")]
    [InlineData("Item2Value", "item2-value")]
    public void ToKebabCase_SplitsWords(string input, string expected)
    {
        Assert.Equal(expected, Naming.ToKebabCase(input));
    }

    [Theory]
    [InlineData("Order", "orders")]
    [InlineData("BeansWidget", "beans-widgets")]
    [InlineData("Category", "categories")]
    [InlineData("Day", "days")]
    [InlineData("Box", "boxes")]
    [InlineData("Address", "addresses")]
    public void DefaultRoute_IsKebabPlural(string input, string expected)
    {
        Assert.Equal(expected, Naming.DefaultRoute(input));
    }

    [Fact]
    public void ToCamelCase_LowersFirstLetter()
    {
        Assert.Equal("beansWidget", Naming.ToCamelCase("BeansWidget"));
    }
}
