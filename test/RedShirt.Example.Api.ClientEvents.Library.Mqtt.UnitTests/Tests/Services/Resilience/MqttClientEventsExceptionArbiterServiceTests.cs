using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using System.Net.Sockets;
using System.Text.Json;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Services.Resilience;

public class MqttClientEventsExceptionArbiterServiceTests
{
    private readonly MqttClientEventsExceptionArbiterService _sut = new();

    [Fact]
    public void GetReport_ApiClientEventsException_IsAlreadyHandled()
    {
        var report = _sut.GetReport(new ApiClientEventsException("done")
        {
            IsHandled = true,
            CouldBeTransient = true
        });

        Assert.True(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_ApiClientEventsException_NonTransient_IsExpected()
    {
        var report = _sut.GetReport(new ApiClientEventsException("config")
        {
            IsHandled = false,
            CouldBeTransient = false
        });

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_ApiClientEventsException_Transient_IsExpected()
    {
        var report = _sut.GetReport(new ApiClientEventsException("timeout")
        {
            IsHandled = false,
            CouldBeTransient = true
        });

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_ApiSecretManagerException_IsAlreadyHandled()
    {
        var report = _sut.GetReport(new ApiSecretManagerException(new InvalidOperationException("ssm"))
        {
            IsHandled = true,
            CouldBeTransient = true,
            CouldBeExternallySolvable = true
        });

        Assert.True(report.AlreadyHandled);
        Assert.True(report.IsExpected);
    }

    [Fact]
    public void GetReport_JsonException_IsExpectedNonTransient()
    {
        var report = _sut.GetReport(new JsonException("bad payload"));

        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_OperationCanceledException_IsNotTransient()
    {
        var report = _sut.GetReport(new OperationCanceledException());

        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_SocketException_IsTransient()
    {
        var report = _sut.GetReport(new SocketException());

        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_TaskCanceledException_IsTransient()
    {
        var report = _sut.GetReport(new TaskCanceledException());

        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_UnknownException_IsUnexpected()
    {
        var report = _sut.GetReport(new InvalidOperationException("surprise"));

        Assert.False(report.IsExpected);
        Assert.False(report.CouldBeTransient);
    }

    [Fact]
    public void GetReport_UnwrapsSingleAggregateException()
    {
        var report = _sut.GetReport(new AggregateException(new SocketException()));

        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
    }
}