using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Data;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Factories;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.UnitTests.Support;

internal sealed class SqliteBackgroundJobDbContextFactory : IBackgroundJobDbContextFactory, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BackgroundJobDbContext> _options;

    public SqliteBackgroundJobDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<BackgroundJobDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new BackgroundJobDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public Task<BackgroundJobDbContext> CreateDbContextAsync(string connectionStringName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new BackgroundJobDbContext(_options));
    }
}

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return UtcNow;
    }

    public void Advance(TimeSpan span)
    {
        UtcNow = UtcNow.Add(span);
    }
}

internal sealed class BackgroundJobServiceFixture : IDisposable
{
    public BackgroundJobServiceFixture(BackgroundJobOptions? options = null)
    {
        Options = options ?? new BackgroundJobOptions
        {
            LeaseSeconds = 60,
            MaxAttempts = 3,
            RetryDelaySeconds = 10,
            MaxRetryDelaySeconds = 25
        };
        Service = new BackgroundJobService(
            DbContextFactory,
            WakeSignal.Object,
            TimeProvider,
            Microsoft.Extensions.Options.Options.Create(Options));
    }

    public SqliteBackgroundJobDbContextFactory DbContextFactory { get; } = new();
    public MutableTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
    public Mock<IBackgroundJobWakeSignal> WakeSignal { get; } = new();
    public BackgroundJobOptions Options { get; }
    public BackgroundJobService Service { get; }

    public void Dispose()
    {
        DbContextFactory.Dispose();
    }
}
