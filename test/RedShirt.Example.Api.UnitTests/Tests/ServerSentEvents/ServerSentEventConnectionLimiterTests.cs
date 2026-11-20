using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.ServerSentEvents;

namespace RedShirt.Example.Api.UnitTests.Tests.ServerSentEvents;

public class ServerSentEventConnectionLimiterTests
{
    private static ServerSentEventConnectionLimiter Limiter(int maxConnectionsPerUser)
    {
        return new ServerSentEventConnectionLimiter(Options.Create(new ServerSentEventsOptions
        {
            MaxConnectionsPerUser = maxConnectionsPerUser
        }));
    }

    [Fact]
    public void Dispose_FreesSlot()
    {
        var limiter = Limiter(1);
        Assert.True(limiter.TryAcquire("user-1", out var first));
        Assert.False(limiter.TryAcquire("user-1", out _));

        first.Dispose();

        Assert.Equal(0, limiter.GetOpenCount("user-1"));
        Assert.True(limiter.TryAcquire("user-1", out _));
    }

    [Fact]
    public void Dispose_Twice_FreesOnlyOneSlot()
    {
        var limiter = Limiter(2);
        Assert.True(limiter.TryAcquire("user-1", out var first));
        Assert.True(limiter.TryAcquire("user-1", out _));

        first.Dispose();
        first.Dispose();

        Assert.Equal(1, limiter.GetOpenCount("user-1"));
        Assert.True(limiter.TryAcquire("user-1", out _));
        Assert.False(limiter.TryAcquire("user-1", out _));
    }

    [Fact]
    public void TryAcquire_AtLimit_IsRefused()
    {
        var limiter = Limiter(3);

        Assert.True(limiter.TryAcquire("user-1", out _));
        Assert.True(limiter.TryAcquire("user-1", out _));
        Assert.True(limiter.TryAcquire("user-1", out _));

        Assert.False(limiter.TryAcquire("user-1", out var refused));
        Assert.Null(refused);
        Assert.Equal(3, limiter.GetOpenCount("user-1"));
    }

    [Fact]
    public async Task TryAcquire_Concurrent_NeverExceedsLimit()
    {
        const int limit = 5;
        var limiter = Limiter(limit);
        using var start = new ManualResetEventSlim();

        var attempts = Enumerable.Range(0, 64)
            .Select(attempt => Task.Run(() =>
            {
                start.Wait(TestContext.Current.CancellationToken);
                return limiter.TryAcquire("user-1", out _);
            }, TestContext.Current.CancellationToken))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(limit, results.Count(acquired => acquired));
        Assert.Equal(limit, limiter.GetOpenCount("user-1"));
    }

    [Fact]
    public void TryAcquire_CountsEachUserSeparately()
    {
        var limiter = Limiter(1);

        Assert.True(limiter.TryAcquire("user-1", out _));
        Assert.True(limiter.TryAcquire("user-2", out _));
        Assert.False(limiter.TryAcquire("user-1", out _));
        Assert.False(limiter.TryAcquire("user-2", out _));
    }

    [Fact]
    public void TryAcquire_UserIdsAreCaseSensitive()
    {
        var limiter = Limiter(1);

        Assert.True(limiter.TryAcquire("user-a", out _));
        Assert.True(limiter.TryAcquire("USER-A", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryAcquire_ZeroOrNegative_IsUnlimited(int maxConnectionsPerUser)
    {
        var limiter = Limiter(maxConnectionsPerUser);

        for (var i = 0; i < 100; i++)
        {
            Assert.True(limiter.TryAcquire("user-1", out _));
        }

        Assert.Equal(0, limiter.GetOpenCount("user-1"));
    }
}