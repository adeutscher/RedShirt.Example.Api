using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Database.Services;
using RedShirt.Example.Api.Upload.Implementation.Data;

namespace RedShirt.Example.Api.Upload.Implementation.Factories;

internal interface IUploadDbContextFactory
{
    Task<UploadDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default);
}

internal sealed class UploadDbContextFactory(IConnectionStringSource connectionStringSource) : IUploadDbContextFactory
{
    public async Task<UploadDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        var connectionString =
            await connectionStringSource.GetConnectionStringAsync(connectionStringName, cancellationToken);
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new UploadDbContext(options);
    }
}