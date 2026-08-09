using System.Text.Json;
using Moq;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;

namespace RedShirt.Example.Api.Common.Distributed.UnitTests.Tests.Extensions;

public class RemoteCacheExtensionsTests
{
    private sealed class SampleObject
    {
        public required string Name { get; init; }
        public required int Count { get; init; }
    }

    [Fact]
    public async Task GetObjectAsync_DeserializesCachedJson()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();
        var expected = new SampleObject { Name = "alpha", Count = 7 };
        var json = JsonSerializer.Serialize(expected);

        remoteCache.Setup(c => c.GetStringAsync(key, TestContext.Current.CancellationToken))
            .ReturnsAsync(json);

        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expected.Name, result.Name);
        Assert.Equal(expected.Count, result.Count);
        remoteCache.Verify(c => c.GetStringAsync(key, TestContext.Current.CancellationToken), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task GetObjectAsync_ReturnsNull_WhenCachedValueIsMissingOrWhitespace(string? cachedValue)
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();

        remoteCache.Setup(c => c.GetStringAsync(key, TestContext.Current.CancellationToken))
            .ReturnsAsync(cachedValue);

        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, TestContext.Current.CancellationToken);

        Assert.Null(result);
        remoteCache.Verify(c => c.GetStringAsync(key, TestContext.Current.CancellationToken), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetObjectAsync_ReturnsNull_WhenCachedValueIsInvalidJson()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();

        remoteCache.Setup(c => c.GetStringAsync(key, TestContext.Current.CancellationToken))
            .ReturnsAsync("{ not-json");

        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, TestContext.Current.CancellationToken);

        Assert.Null(result);
        remoteCache.Verify(c => c.GetStringAsync(key, TestContext.Current.CancellationToken), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetObjectAsync_ReturnsNull_WhenCachedJsonIsNullLiteral()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();

        remoteCache.Setup(c => c.GetStringAsync(key, TestContext.Current.CancellationToken))
            .ReturnsAsync("null");

        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, TestContext.Current.CancellationToken);

        Assert.Null(result);
        remoteCache.Verify(c => c.GetStringAsync(key, TestContext.Current.CancellationToken), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetObjectAsync_PassesCancellationTokenToRemoteCache()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();
        using var cts = new CancellationTokenSource();

        remoteCache.Setup(c => c.GetStringAsync(key, cts.Token))
            .ReturnsAsync("""{"Name":"beta","Count":3}""");

        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, cts.Token);

        Assert.NotNull(result);
        Assert.Equal("beta", result.Name);
        Assert.Equal(3, result.Count);
        remoteCache.Verify(c => c.GetStringAsync(key, cts.Token), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetObjectAsync_SerializesAndStoresJson()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();
        var value = new SampleObject { Name = "gamma", Count = 11 };
        var expiry = TimeSpan.FromMinutes(5);
        string? capturedJson = null;

        remoteCache.Setup(c => c.SetStringAsync(key, It.IsAny<string?>(), expiry, TestContext.Current.CancellationToken))
            .Callback<string, string?, TimeSpan, CancellationToken>((_, json, _, _) => capturedJson = json)
            .Returns(Task.CompletedTask);

        await remoteCache.Object.SetObjectAsync(key, value, expiry, TestContext.Current.CancellationToken);

        Assert.Equal(JsonSerializer.Serialize(value), capturedJson);
        remoteCache.Verify(c => c.SetStringAsync(key, It.IsAny<string?>(), expiry, TestContext.Current.CancellationToken),
            Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetObjectAsync_PassesCancellationTokenToRemoteCache()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();
        var value = new SampleObject { Name = "delta", Count = 2 };
        var expiry = TimeSpan.FromSeconds(30);
        using var cts = new CancellationTokenSource();

        remoteCache.Setup(c => c.SetStringAsync(key, JsonSerializer.Serialize(value), expiry, cts.Token))
            .Returns(Task.CompletedTask);

        await remoteCache.Object.SetObjectAsync(key, value, expiry, cts.Token);

        remoteCache.Verify(c => c.SetStringAsync(key, JsonSerializer.Serialize(value), expiry, cts.Token), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetObjectAsync_ThenGetObjectAsync_RoundTripsObject()
    {
        var remoteCache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var key = Guid.NewGuid().ToString();
        var value = new SampleObject { Name = "epsilon", Count = 42 };
        var expiry = TimeSpan.FromHours(1);
        string? storedJson = null;

        remoteCache.Setup(c => c.SetStringAsync(key, It.IsAny<string?>(), expiry, TestContext.Current.CancellationToken))
            .Callback<string, string?, TimeSpan, CancellationToken>((_, json, _, _) => storedJson = json)
            .Returns(Task.CompletedTask);
        remoteCache.Setup(c => c.GetStringAsync(key, TestContext.Current.CancellationToken))
            .ReturnsAsync(() => storedJson);

        await remoteCache.Object.SetObjectAsync(key, value, expiry, TestContext.Current.CancellationToken);
        var result = await remoteCache.Object.GetObjectAsync<SampleObject>(key, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Count, result.Count);
        remoteCache.Verify(c => c.SetStringAsync(key, It.IsAny<string?>(), expiry, TestContext.Current.CancellationToken),
            Times.Once);
        remoteCache.Verify(c => c.GetStringAsync(key, TestContext.Current.CancellationToken), Times.Once);
        remoteCache.VerifyNoOtherCalls();
    }
}
