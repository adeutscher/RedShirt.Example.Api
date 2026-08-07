using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Extensions.Messages;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Constants;
using RedShirt.Example.Api.Common.RateLimiting.RateLimiters;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.RateLimiters;

public class RedisSlidingWindowRateLimiterTests
{
    private static RedisSlidingWindowRateLimiter CreateLimiter(
        IDatabase redis,
        IHttpContextAccessor accessor,
        int permitLimit,
        bool failClosed)
    {
        return new RedisSlidingWindowRateLimiter(
            redis,
            "rate:test",
            permitLimit,
            TimeSpan.FromMinutes(1),
            failClosed,
            accessor,
            Mock.Of<ILogger>());
    }

    private static RedisResult CreateScriptResult(long allowed, long remaining, long retryAfterMs)
    {
        return RedisResult.Create(
        [
            RedisResult.Create(allowed),
            RedisResult.Create(remaining),
            RedisResult.Create(retryAfterMs)
        ]);
    }

    public class AcquireAsync
    {
        [Fact]
        public async Task Allows_AndExposesRemainingMetadata_WhenScriptAllows()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(CreateScriptResult(1, 4, 0));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var httpContext = new DefaultHttpContext();
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 5, true);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.True(lease.IsAcquired);
            Assert.True(lease.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remaining));
            Assert.Equal(4, remaining);
            Assert.True(lease.TryGetMetadata(RateLimitMetadata.PermitLimit, out var limit));
            Assert.Equal(5, limit);
            Assert.Contains(RateLimitMetadata.RemainingPermits.Name, lease.MetadataNames);
            Assert.Contains(RateLimitMetadata.PermitLimit.Name, lease.MetadataNames);
        }

        [Fact]
        public async Task Denies_WithRetryAfterMetadata_WhenScriptRejects()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(CreateScriptResult(0, 0, 1500));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 2, true);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.False(lease.IsAcquired);
            Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
            Assert.Equal(TimeSpan.FromMilliseconds(1500), retryAfter);
            Assert.True(lease.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remaining));
            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task FailsClosed_WhenRedisThrows_AndFailClosedTrue()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.Unknown));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 3, true);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.False(lease.IsAcquired);
            Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
            Assert.Equal(TimeSpan.FromSeconds(1), retryAfter);
        }

        [Fact]
        public async Task FailsClosed_WhenScriptResultTooShort_AndFailClosedTrue()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create(2)
                ]));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 3, true);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.False(lease.IsAcquired);
        }

        [Fact]
        public async Task FailsOpen_WhenRedisThrows_AndFailClosedFalse()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ThrowsAsync(new RedisException("boom"));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 3, false);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.True(lease.IsAcquired);
        }

        [Fact]
        public async Task Rejects_WhenPermitCountGreaterThanOne()
        {
            var redis = new Mock<IDatabase>(MockBehavior.Strict);
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 5, true);

            using var lease = await limiter.AcquireAsync(2, TestContext.Current.CancellationToken);

            Assert.False(lease.IsAcquired);
            Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
            Assert.Equal(TimeSpan.Zero, retryAfter);
        }

        [Fact]
        public async Task SkipsHeaders_WhenHttpContextMissing()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(CreateScriptResult(1, 2, 0));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 5, true);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.True(lease.IsAcquired);
        }

        [Fact]
        public async Task Throws_WhenPermitCountLessThanOne()
        {
            var redis = new Mock<IDatabase>(MockBehavior.Strict);
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var limiter = CreateLimiter(redis.Object, accessor.Object, 5, true);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                limiter.AcquireAsync(0, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public class GetStatistics
    {
        [Fact]
        public async Task DecrementsAvailable_WhileLeaseHeld_AndRestoresOnDispose()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(CreateScriptResult(1, 1, 0));

            var redis = new Mock<IDatabase>(MockBehavior.Strict);

            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = CreateLimiter(redis.Object, accessor.Object, 5, true);

            Assert.Equal(5, limiter.GetStatistics()!.CurrentAvailablePermits);

            var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);
            Assert.Equal(4, limiter.GetStatistics()!.CurrentAvailablePermits);

            lease.Dispose();
            Assert.Equal(5, limiter.GetStatistics()!.CurrentAvailablePermits);
        }
    }

    public class IdleDuration
    {
        [Fact]
        public void IsNull()
        {
            var redis = new Mock<IDatabase>(MockBehavior.Strict);
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var limiter = CreateLimiter(redis.Object, accessor.Object, 1, true);

            Assert.Null(limiter.IdleDuration);
        }
    }
}