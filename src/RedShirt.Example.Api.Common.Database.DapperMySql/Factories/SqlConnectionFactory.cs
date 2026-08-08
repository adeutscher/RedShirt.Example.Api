using MySql.Data.MySqlClient;
using RedShirt.Example.Api.Common.Database.Services;
using System.Data;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Factories;

public interface ISqlConnectionFactory
{
    Task<IDbConnection> GetMySqlConnectionAsync(string name, CancellationToken cancellationToken = default);
}

internal class SqlConnectionFactory(IConnectionStringSource connectionStringSource, string connectionStringPath)
    : ISqlConnectionFactory
{
    public async Task<IDbConnection> GetMySqlConnectionAsync(string name, CancellationToken cancellationToken = default)
    {
        var connectionString = await connectionStringSource.GetConnectionStringAsync(name, cancellationToken);
        var connection = new MySqlConnection(connectionString);
        return connection;
    }
}