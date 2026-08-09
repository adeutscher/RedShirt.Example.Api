namespace RedShirt.Example.Api.Common.Database.DapperMySql.Utility;

public static class SqlQueryHelper
{
    public static string EncodeTermForLike(string term)
    {
        // https://stackoverflow.com/questions/6030099/does-dapper-support-the-like-operator
        return "%" + term.Replace("[", "[[]").Replace("%", "[%]") + "%";
    }
}