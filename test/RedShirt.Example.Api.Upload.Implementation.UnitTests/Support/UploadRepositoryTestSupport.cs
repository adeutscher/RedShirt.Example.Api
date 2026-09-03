using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.Upload.Implementation.Data;
using RedShirt.Example.Api.Upload.Implementation.Factories;
using RedShirt.Example.Api.Upload.Implementation.Repositories;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

internal sealed class InMemoryRemoteCacheService : IRemoteCacheService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetStringAsync(string key, string? value, TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }

        return Task.CompletedTask;
    }
}

internal sealed class SqliteUploadDbContextFactory : IUploadDbContextFactory, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<UploadDbContext> _options;

    public SqliteUploadDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new UploadDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public Task<UploadDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UploadDbContext(_options));
    }
}

internal static class UploadRepositoryTestSupport
{
    internal static UploadRepository CreateRepository(
        IUploadDbContextFactory dbContextFactory,
        IRemoteCacheService? cacheService = null)
    {
        return new UploadRepository(
            dbContextFactory,
            cacheService ?? new InMemoryRemoteCacheService());
    }
}