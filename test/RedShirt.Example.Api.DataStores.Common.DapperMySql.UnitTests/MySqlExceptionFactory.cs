using MySql.Data.MySqlClient;
using System.Reflection;

namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.UnitTests;

internal static class MySqlExceptionFactory
{
    private static readonly ConstructorInfo NumberConstructor =
        typeof(MySqlException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(string), typeof(int)],
            null)
        ?? throw new InvalidOperationException("MySqlException(string, int) constructor not found.");

    public static MySqlException Create(string message, int number)
    {
        return (MySqlException) NumberConstructor.Invoke([message, number]);
    }
}