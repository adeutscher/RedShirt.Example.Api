using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.DataStores.Common.Services;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Data;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Factories;

/// <summary>
///     Creates <see cref="CustomerDbContext" /> instances after resolving a named connection string.
///     Connection strings are loaded asynchronously from the secret manager.
/// </summary>
internal interface ICustomerDbContextFactory
{
    /// <summary>
    ///     Create a new <see cref="CustomerDbContext" /> using the connection string registered under
    ///     <paramref name="connectionStringName" />.
    ///     The caller owns the returned context and must dispose it.
    /// </summary>
    Task<CustomerDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default);
}

internal sealed class CustomerDbContextFactory(IConnectionStringSource connectionStringSource)
    : ICustomerDbContextFactory
{
    public async Task<CustomerDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            await connectionStringSource.GetConnectionStringAsync(connectionStringName, cancellationToken);
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new CustomerDbContext(options);
    }
}