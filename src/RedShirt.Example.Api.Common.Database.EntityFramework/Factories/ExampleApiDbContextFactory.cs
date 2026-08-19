using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Database.EntityFramework.Data;
using RedShirt.Example.Api.Common.Database.Services;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.Factories;

/// <summary>
///     Creates <see cref="ExampleApiDbContext" /> instances after resolving a named connection string.
///     Connection strings are loaded asynchronously from the secret manager.
/// </summary>
public interface IExampleApiDbContextFactory
{
    /// <summary>
    ///     Create a new <see cref="ExampleApiDbContext" /> using the connection string registered under
    ///     <paramref name="connectionStringName" />.
    ///     The caller owns the returned context and must dispose it.
    /// </summary>
    Task<ExampleApiDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default);
}

internal sealed class ExampleApiDbContextFactory(IConnectionStringSource connectionStringSource)
    : IExampleApiDbContextFactory
{
    public async Task<ExampleApiDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            await connectionStringSource.GetConnectionStringAsync(connectionStringName, cancellationToken);
        var options = new DbContextOptionsBuilder<ExampleApiDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new ExampleApiDbContext(options);
    }
}
