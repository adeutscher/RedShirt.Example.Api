using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Extensions;

namespace RedShirt.Example.Api.BackgroundJobs.Cleanup.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static BackgroundJobCleanupOptions GetOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddBackgroundJobCleanup(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<BackgroundJobCleanupOptions>>().Value;
    }

    [Fact]
    public void AddBackgroundJobCleanup_BindsSettings()
    {
        var options = GetOptions(new Dictionary<string, string?>
        {
            ["BackgroundJobs:CleanupEnabled"] = "true",
            ["BackgroundJobs:CleanupRetentionDays"] = "14"
        });

        Assert.True(options.CleanupEnabled);
        Assert.Equal(14, options.CleanupRetentionDays);
    }

    [Fact]
    public void AddBackgroundJobCleanup_IsDisabledByDefault_AndNeedsNoRetention()
    {
        var options = GetOptions([]);

        Assert.False(options.CleanupEnabled);
        Assert.Null(options.CleanupRetentionDays);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    public void AddBackgroundJobCleanup_WhenEnabled_RequiresRetentionOfAtLeastOneDay(string? retentionDays)
    {
        Assert.Throws<OptionsValidationException>(() => GetOptions(new Dictionary<string, string?>
        {
            ["BackgroundJobs:CleanupEnabled"] = "true",
            ["BackgroundJobs:CleanupRetentionDays"] = retentionDays
        }));
    }
}
