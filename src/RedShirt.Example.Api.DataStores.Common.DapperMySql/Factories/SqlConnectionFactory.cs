using MySql.Data.MySqlClient;
using RedShirt.Example.Api.DataStores.Common.Services;
using System.Data;

namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.Factories;

public interface ISqlConnectionFactory
{
    Task<IDbConnection> GetMySqlConnectionAsync(string name, CancellationToken cancellationToken = default);
}

internal class SqlConnectionFactory(IConnectionStringSource connectionStringSource)
    : ISqlConnectionFactory
{
    public async Task<IDbConnection> GetMySqlConnectionAsync(string name, CancellationToken cancellationToken = default)
    {
        var connectionString = await connectionStringSource.GetConnectionStringAsync(name, cancellationToken);
        var connection = new MySqlConnection(connectionString);
        return connection;
    }
}