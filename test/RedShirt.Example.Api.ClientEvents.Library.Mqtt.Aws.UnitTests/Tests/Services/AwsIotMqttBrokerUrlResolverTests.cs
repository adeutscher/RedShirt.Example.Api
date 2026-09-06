using Amazon.IoT;
using Amazon.IoT.Model;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Services;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.UnitTests.Tests.Services;

public class AwsIotMqttBrokerUrlResolverTests
{
    private static AwsIotMqttBrokerUrlResolver CreateSut(
        Mock<IAmazonIoT> amazonIoT,
        AwsIotMqttBrokerUrlResolver.ConfigurationModel? configuration = null)
    {
        return new AwsIotMqttBrokerUrlResolver(
            amazonIoT.Object,
            Options.Create(configuration ?? new AwsIotMqttBrokerUrlResolver.ConfigurationModel()));
    }

    [Fact]
    public async Task ResolveBrokerUrlAsync_ReturnsConnectHostAndHostHeader_WhenBrokerConnectHostSet()
    {
        const string endpointAddress = "abc123-ats.iot.us-east-1.ministack:4566";
        var amazonIoT = new Mock<IAmazonIoT>(MockBehavior.Strict);
        amazonIoT
            .Setup(i => i.DescribeEndpointAsync(It.IsAny<DescribeEndpointRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescribeEndpointResponse
            {
                EndpointAddress = endpointAddress
            });

        var sut = CreateSut(amazonIoT, new AwsIotMqttBrokerUrlResolver.ConfigurationModel
        {
            BrokerConnectHost = "ministack"
        });

        var target = await sut.ResolveBrokerUrlAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ws://ministack:4566/mqtt", target.BrokerUrl);
        Assert.Equal(endpointAddress, target.WebSocketHostHeader);
        amazonIoT.VerifyAll();
    }

    [Fact]
    public async Task ResolveBrokerUrlAsync_ReturnsDirectEndpoint_WhenBrokerConnectHostUnset()
    {
        const string endpointAddress = "abc123-ats.iot.us-east-1.amazonaws.com";
        var amazonIoT = new Mock<IAmazonIoT>(MockBehavior.Strict);
        amazonIoT
            .Setup(i => i.DescribeEndpointAsync(
                It.Is<DescribeEndpointRequest>(request => request.EndpointType == "iot:Data-ATS"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescribeEndpointResponse
            {
                EndpointAddress = endpointAddress
            });

        var sut = CreateSut(amazonIoT);

        var target = await sut.ResolveBrokerUrlAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"ws://{endpointAddress}/mqtt", target.BrokerUrl);
        Assert.Null(target.WebSocketHostHeader);
        amazonIoT.VerifyAll();
    }

    [Fact]
    public async Task ResolveBrokerUrlAsync_Throws_WhenEndpointAddressInvalid()
    {
        var amazonIoT = new Mock<IAmazonIoT>(MockBehavior.Strict);
        amazonIoT
            .Setup(i => i.DescribeEndpointAsync(It.IsAny<DescribeEndpointRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescribeEndpointResponse
            {
                EndpointAddress = "://not-a-valid-host"
            });

        var sut = CreateSut(amazonIoT);

        var exception = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.ResolveBrokerUrlAsync(TestContext.Current.CancellationToken));

        Assert.False(exception.CouldBeTransient);
        amazonIoT.VerifyAll();
    }

    [Fact]
    public async Task ResolveBrokerUrlAsync_Throws_WhenEndpointAddressMissing()
    {
        var amazonIoT = new Mock<IAmazonIoT>(MockBehavior.Strict);
        amazonIoT
            .Setup(i => i.DescribeEndpointAsync(It.IsAny<DescribeEndpointRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescribeEndpointResponse
            {
                EndpointAddress = null
            });

        var sut = CreateSut(amazonIoT);

        var exception = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.ResolveBrokerUrlAsync(TestContext.Current.CancellationToken));

        Assert.True(exception.CouldBeTransient);
        amazonIoT.VerifyAll();
    }
}