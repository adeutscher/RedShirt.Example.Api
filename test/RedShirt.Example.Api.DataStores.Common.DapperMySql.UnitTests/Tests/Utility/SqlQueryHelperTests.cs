using RedShirt.Example.Api.DataStores.Common.DapperMySql.Utility;

namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.UnitTests.Tests.Utility;

public class SqlQueryHelperTests
{
    [Theory]
    [InlineData("abc", "%abc%")]
    [InlineData("", "%%")]
    [InlineData("50%", "%50!%%")]
    [InlineData("a_b", "%a!_b%")]
    [InlineData("wow!", "%wow!!%")]
    [InlineData("!%_", "%!!!%!_%")]
    [InlineData(@"back\slash [x]", @"%back\slash [x]%")]
    public void EncodeTermForLike_EscapesWildcardsAndWrapsForContains(string term, string expected)
    {
        Assert.Equal(expected, SqlQueryHelper.EncodeTermForLike(term));
    }

    [Fact]
    public void LikeEscapeClause_NamesTheEscapeCharacter()
    {
        // LikeEscapeClause is the constant under test, so it is deliberately the 'actual' argument.
        // xUnit2000 assumes any constant is the expected value, which does not apply here.
#pragma warning disable xUnit2000
        Assert.Equal($"ESCAPE '{SqlQueryHelper.LikeEscapeCharacter}'", SqlQueryHelper.LikeEscapeClause);
#pragma warning restore xUnit2000
    }
}