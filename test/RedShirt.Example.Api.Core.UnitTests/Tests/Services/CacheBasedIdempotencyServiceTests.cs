using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.Distributed.Models;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.Services;

public class CacheBasedIdempotencyServiceTests
{
    private static CacheBasedIdempotencyService CreateService(
        IRemoteCacheService? dataCacheService = null,
        IAbstractedLockService? lockService = null,
        int idempotencyTimeMinutes = 10)
    {
        return new CacheBasedIdempotencyService(
            dataCacheService ?? new Mock<IRemoteCacheService>(MockBehavior.Strict).Object,
            lockService ?? new Mock<IAbstractedLockService>(MockBehavior.Strict).Object,
            Options.Create(new CacheBasedIdempotencyService.ConfigurationModel
            {
                IdempotencyTrackingTimeMinutes = idempotencyTimeMinutes
            }));
    }

    public class ConfigurationModel
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(-10, 1)]
        [InlineData(1, 1)]
        [InlineData(30, 30)]
        public void EffectiveIdempotencyTimeMinutes_EnforcesMinimumOfOne(
            int configuredMinutes,
            int expectedEffectiveMinutes)
        {
            var config = new CacheBasedIdempotencyService.ConfigurationModel
            {
                IdempotencyTrackingTimeMinutes = configuredMinutes
            };

            Assert.Equal(expectedEffectiveMinutes, config.EffectiveIdempotencyTimeMinutes);
        }
    }

    public class GetLockAsync
    {
        [Fact]
        public async Task PrefixesKey_AndReturnsLockFromLockService()
        {
            var expectedLock = new Mock<IAbstractedLock>(MockBehavior.Strict);
            var lockService = new Mock<IAbstractedLockService>(MockBehavior.Strict);
            lockService
                .Setup(s => s.GetLockAsync("idempotent-concurrency:submission-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLock.Object);

            var service = CreateService(lockService: lockService.Object);

            var result = await service.GetLockAsync("submission-1", TestContext.Current.CancellationToken);

            Assert.Same(expectedLock.Object, result);
            lockService.Verify(
                s => s.GetLockAsync("idempotent-concurrency:submission-1", It.IsAny<CancellationToken>()),
                Times.Once);
            lockService.VerifyNoOtherCalls();
        }
    }

    public class GetRecordAsync
    {
        [Fact]
        public async Task DeserializesStoredJson()
        {
            var payload = new SampleRecord("alpha", 3);
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync("key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(JsonSerializer.Serialize(payload));

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>("key", TestContext.Current.CancellationToken);

            Assert.Equal(payload, result);
            cache.Verify(c => c.GetStringAsync("key", It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnsDefault_WhenCacheMisses()
        {
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync("missing", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?) null);

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>("missing", TestContext.Current.CancellationToken);

            Assert.Null(result);
            cache.Verify(c => c.GetStringAsync("missing", It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnsDefault_WhenStoredJsonIsInvalid()
        {
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync("key", It.IsAny<CancellationToken>()))
                .ReturnsAsync("{not-json");

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>("key", TestContext.Current.CancellationToken);

            Assert.Null(result);
            cache.Verify(c => c.GetStringAsync("key", It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }
    }

    public class SetRecordAsync
    {
        [Fact]
        public async Task SerializesValue_AndUsesConfiguredExpiration()
        {
            var payload = new SampleRecord("beta", 9);
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.SetStringAsync(
                    "key",
                    JsonSerializer.Serialize(payload),
                    TimeSpan.FromMinutes(15),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateService(cache.Object, idempotencyTimeMinutes: 15);

            await service.SetRecordAsync("key", payload, TestContext.Current.CancellationToken);

            cache.Verify(c => c.SetStringAsync(
                "key",
                JsonSerializer.Serialize(payload),
                TimeSpan.FromMinutes(15),
                It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task UsesMinimumOneMinuteExpiration_WhenConfiguredBelowOne(int idempotencyTimeMinutes)
        {
            var payload = new SampleRecord("gamma", 1);
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.SetStringAsync(
                    "key",
                    JsonSerializer.Serialize(payload),
                    TimeSpan.FromMinutes(1),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateService(cache.Object, idempotencyTimeMinutes: idempotencyTimeMinutes);

            await service.SetRecordAsync("key", payload, TestContext.Current.CancellationToken);

            cache.Verify(c => c.SetStringAsync(
                "key",
                JsonSerializer.Serialize(payload),
                TimeSpan.FromMinutes(1),
                It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }
    }

    private sealed record SampleRecord(string Name, int Count);
}