using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Data;
using RedShirt.Example.Api.DataStores.Common.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.Factories;

internal interface IBackgroundJobDbContextFactory
{
    Task<BackgroundJobDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default);
}

internal sealed class BackgroundJobDbContextFactory(IConnectionStringSource connectionStringSource)
    : IBackgroundJobDbContextFactory
{
    public async Task<BackgroundJobDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            await connectionStringSource.GetConnectionStringAsync(connectionStringName, cancellationToken);
        var options = new DbContextOptionsBuilder<BackgroundJobDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new BackgroundJobDbContext(options);
    }
}
