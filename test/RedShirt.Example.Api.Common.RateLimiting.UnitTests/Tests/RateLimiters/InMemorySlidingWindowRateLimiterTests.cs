using Microsoft.AspNetCore.Http;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Constants;
using RedShirt.Example.Api.Common.RateLimiting.RateLimiters;
using System.Threading.RateLimiting;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.RateLimiters;

public class InMemorySlidingWindowRateLimiterTests
{
    public class AcquireAsync
    {
        [Fact]
        public async Task Allows_UntilPermitLimit_ThenDeniesWithRetryAfter()
        {
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);

            var limiter = new InMemorySlidingWindowRateLimiter(2, TimeSpan.FromMinutes(1), accessor.Object);

            using var first = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);
            using var second = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);
            using var third = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.True(first.IsAcquired);
            Assert.True(first.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remainingAfterFirst));
            Assert.Equal(1, remainingAfterFirst);

            Assert.True(second.IsAcquired);
            Assert.True(second.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remainingAfterSecond));
            Assert.Equal(0, remainingAfterSecond);

            Assert.False(third.IsAcquired);
            Assert.True(third.TryGetMetadata(RateLimitMetadata.RemainingPermits, out var remainingAfterThird));
            Assert.Equal(0, remainingAfterThird);
            Assert.True(third.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
            Assert.True(retryAfter > TimeSpan.Zero);
            Assert.True(third.TryGetMetadata(RateLimitMetadata.PermitLimit, out var limit));
            Assert.Equal(2, limit);
        }

        [Fact]
        public async Task Rejects_WhenPermitCountGreaterThanOne()
        {
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var limiter = new InMemorySlidingWindowRateLimiter(5, TimeSpan.FromMinutes(1), accessor.Object);

            using var lease = await limiter.AcquireAsync(2, TestContext.Current.CancellationToken);

            Assert.False(lease.IsAcquired);
            Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
            Assert.Equal(TimeSpan.Zero, retryAfter);
        }

        [Fact]
        public async Task Throws_WhenPermitCountLessThanOne()
        {
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var limiter = new InMemorySlidingWindowRateLimiter(5, TimeSpan.FromMinutes(1), accessor.Object);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                limiter.AcquireAsync(0, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public class GetStatistics
    {
        [Fact]
        public async Task ReflectsAvailablePermits_AfterAcquires()
        {
            var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?) null);
            var limiter = new InMemorySlidingWindowRateLimiter(3, TimeSpan.FromMinutes(1), accessor.Object);

            Assert.Equal(3, limiter.GetStatistics()!.CurrentAvailablePermits);

            using var lease = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

            Assert.Equal(2, limiter.GetStatistics()!.CurrentAvailablePermits);
        }
    }
}