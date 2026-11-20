using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.Extensions;
using RedShirt.Example.Api.ServerSentEvents;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class ServerSentEventsExtensionsTests
{
    private static (WebApplication App, CapturingLoggerProvider Logs) BuildApp(string environmentName,
        string? maxConnectionsPerUser)
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServerSentEvents:MaxConnectionsPerUser"] = maxConnectionsPerUser
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddServerSentEventConnectionLimits(builder.Configuration);
        return (builder.Build(), logs);
    }

    [Fact]
    public void AddServerSentEventConnectionLimits_BindsOptionsAndRegistersSingletonLimiter()
    {
        var (app, _) = BuildApp("Production", "3");
        using var disposableApp = app;

        Assert.Equal(3, app.Services.GetRequiredService<IOptions<ServerSentEventsOptions>>().Value
            .MaxConnectionsPerUser);
        Assert.Same(
            app.Services.GetRequiredService<IServerSentEventConnectionLimiter>(),
            app.Services.GetRequiredService<IServerSentEventConnectionLimiter>());
    }

    [Theory]
    [InlineData("Production", null, true)]
    [InlineData("Production", "0", true)]
    [InlineData("Production", "-1", true)]
    [InlineData("Development", null, true)]
    [InlineData("Production", "1", false)]
    [InlineData("Development", "3", false)]
    public void WarnIfServerSentEventConnectionsUnlimited_WarnsOnlyWhenUnlimited(string environmentName,
        string? maxConnectionsPerUser, bool expectWarning)
    {
        var (app, logs) = BuildApp(environmentName, maxConnectionsPerUser);
        using var disposableApp = app;

        var returned = app.WarnIfServerSentEventConnectionsUnlimited();

        Assert.Same(app, returned);
        Assert.Equal(expectWarning, logs.Entries.Any(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message == ServerSentEventsExtensions.UnlimitedConnectionsWarning));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(Entries);
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}