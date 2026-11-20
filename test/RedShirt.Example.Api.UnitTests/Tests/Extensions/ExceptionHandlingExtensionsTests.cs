using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.Extensions;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class ExceptionHandlingExtensionsTests
{
    private static List<(LogLevel Level, string Message)> StartAndCaptureWarnings(string? expose,
        string environmentName)
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName = environmentName});
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExceptionHandling:ExposeUpstreamExceptionMessages"] = expose
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddApiExceptionHandlingOptions(builder.Configuration);
        using var app = builder.Build();

        Assert.Same(app, app.WarnAboutExceptionMessageExposure());

        return logs.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void WarnAboutExceptionMessageExposure_IsSilent_WhenSettingIsOff(string? expose)
    {
        Assert.Empty(StartAndCaptureWarnings(expose, Environments.Development));
    }

    [Fact]
    public void WarnAboutExceptionMessageExposure_WarnsThatMessagesAreExposed_InDevelopment()
    {
        var warning = Assert.Single(StartAndCaptureWarnings("true", Environments.Development));

        Assert.Equal(ExceptionHandlingExtensions.ExposingWarning, warning.Message);
    }

    [Fact]
    public void WarnAboutExceptionMessageExposure_WarnsThatSettingIsIgnored_OutsideDevelopment()
    {
        var warning = Assert.Single(StartAndCaptureWarnings("true", Environments.Production));

        Assert.Equal(ExceptionHandlingExtensions.IgnoredWarning, warning.Message);
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