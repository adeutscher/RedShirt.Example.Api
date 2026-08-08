namespace RedShirt.Example.Api.Common.Database.DapperMySql.Utility;

public static class DatabaseUtility
{
    public static string QuoteResource(string input)
    {
        return $"`{input}`";
    }
}