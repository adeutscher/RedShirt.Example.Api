using Amazon.IoT;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Extensions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Services;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.UnitTests.Support;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAwsMqttClientEvents_RegistersAmazonIoTClient()
    {
        var configuration = new ConfigurationBuilder().Build();

        TestUtilities.WrapLocalAwsEnvironment(() =>
        {
            var provider = new ServiceCollection()
                .AddAwsMqttClientEvents(configuration)
                .BuildServiceProvider();

            Assert.NotNull(provider.GetService<IAmazonIoT>());
        });
    }

    [Fact]
    public void AddAwsMqttClientEvents_RegistersBrokerUrlResolver()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClientEvents:Mqtt:AWS:BrokerConnectHost"] = "ministack"
            })
            .Build();

        TestUtilities.WrapLocalAwsEnvironment(() =>
        {
            var provider = new ServiceCollection()
                .AddAwsMqttClientEvents(configuration)
                .BuildServiceProvider();

            var resolver = provider.GetRequiredService<IMqttBrokerUrlResolver>();
            Assert.IsType<MqttBrokerUrlResolver>(resolver);
        });
    }
}