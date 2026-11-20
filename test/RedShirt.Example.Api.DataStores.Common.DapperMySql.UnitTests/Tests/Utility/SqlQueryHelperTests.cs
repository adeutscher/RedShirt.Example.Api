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
        Assert.Equal($"ESCAPE '{SqlQueryHelper.LikeEscapeCharacter}'", SqlQueryHelper.LikeEscapeClause);
    }
}