using Microsoft.Extensions.Configuration;
using RedShirt.Example.Api.Extensions;

namespace RedShirt.Example.Api.UnitTests.Tests.Extensions;

public class ConfigurationBuilderExtensionTests
{
    [Theory]
    [InlineData("A", "A", "B")]
    [InlineData("A__B", "A:B", "C")]
    [InlineData("X__Y", "X__Y", "Z")]
    [InlineData("C", "C", "D")]
    public void Test_AddEnvironmentVariablesWithSegmentSupport(string environmentKey, string configurationKey,
        string value)
    {
        var environment = new Dictionary<string, string>
        {
            [environmentKey] = value
        };

        TestUtilities.WrapEnvironment(environment, () =>
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariablesWithSegmentSupport()
                .Build();

            Assert.Equal(configuration[configurationKey], value);
        });
    }
}