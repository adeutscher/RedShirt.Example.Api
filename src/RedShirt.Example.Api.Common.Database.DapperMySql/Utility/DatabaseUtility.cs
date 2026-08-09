namespace RedShirt.Example.Api.Common.Database.DapperMySql.Utility;

public static class DatabaseUtility
{
    /// <summary>
    ///     Quote a resource in MySQL style with backticks.
    ///     If you wanted to adapt this library to support MS SQL (which prefers square brackets),
    ///     then this would be the place to do it.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string QuoteResource(string input)
    {
        return $"`{input}`";
    }
}