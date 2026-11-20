using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.Extensions;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class HostFilteringExtensionsTests
{
    [Theory]
    [InlineData("Production", null, true)]
    [InlineData("Production", "*", true)]
    [InlineData("Staging", "api.example.com;*", true)]
    [InlineData("Production", "api.example.com", false)]
    [InlineData("Production", "api.example.com;internal.example.com", false)]
    [InlineData("Development", null, false)]
    [InlineData("Development", "*", false)]
    public void WarnIfAllHostsAllowed_WarnsOnlyForAnyHostOutsideDevelopment(string environmentName,
        string? allowedHosts, bool expectWarning)
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        // The project's appsettings files are not loaded here, so only this value applies.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = allowedHosts
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        using var app = builder.Build();

        var returned = app.WarnIfAllHostsAllowed();

        Assert.Same(app, returned);
        Assert.Equal(expectWarning, logs.Entries.Any(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message == HostFilteringExtensions.AllHostsAllowedWarning));
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