using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.Distributed.Models;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.Services;

public class CacheBasedIdempotencyServiceTests
{
    private const string RecordKey = "idempotent-record:CreateThingCommand:user-1:key";
    private static readonly IdempotencyScope Scope = new("CreateThingCommand", "user-1", "key");

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
        public async Task PrefixesScopedKey_AndReturnsLockFromLockService()
        {
            var expectedLock = new Mock<IAbstractedLock>(MockBehavior.Strict);
            var lockService = new Mock<IAbstractedLockService>(MockBehavior.Strict);
            lockService
                .Setup(s => s.GetLockAsync("idempotent-concurrency:CreateThingCommand:user-1:submission-1",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLock.Object);

            var service = CreateService(lockService: lockService.Object);

            var result = await service.GetLockAsync(Scope with {Key = "submission-1"},
                TestContext.Current.CancellationToken);

            Assert.Same(expectedLock.Object, result);
            lockService.Verify(
                s => s.GetLockAsync("idempotent-concurrency:CreateThingCommand:user-1:submission-1",
                    It.IsAny<CancellationToken>()),
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
                .Setup(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(JsonSerializer.Serialize(payload));

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>(Scope, TestContext.Current.CancellationToken);

            Assert.Equal(payload, result);
            cache.Verify(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnsDefault_WhenCacheMisses()
        {
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?) null);

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>(Scope, TestContext.Current.CancellationToken);

            Assert.Null(result);
            cache.Verify(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnsDefault_WhenStoredJsonIsInvalid()
        {
            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync("{not-json");

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>(Scope, TestContext.Current.CancellationToken);

            Assert.Null(result);
            cache.Verify(c => c.GetStringAsync(RecordKey, It.IsAny<CancellationToken>()), Times.Once);
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
                    RecordKey,
                    JsonSerializer.Serialize(payload),
                    TimeSpan.FromMinutes(15),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateService(cache.Object, idempotencyTimeMinutes: 15);

            await service.SetRecordAsync(Scope, payload, TestContext.Current.CancellationToken);

            cache.Verify(c => c.SetStringAsync(
                RecordKey,
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
                    RecordKey,
                    JsonSerializer.Serialize(payload),
                    TimeSpan.FromMinutes(1),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateService(cache.Object, idempotencyTimeMinutes: idempotencyTimeMinutes);

            await service.SetRecordAsync(Scope, payload, TestContext.Current.CancellationToken);

            cache.Verify(c => c.SetStringAsync(
                RecordKey,
                JsonSerializer.Serialize(payload),
                TimeSpan.FromMinutes(1),
                It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }
    }

    public class KeyScoping
    {
        [Theory]
        [InlineData("CreateThingCommand", "user-2", "key")]
        [InlineData("CreateOtherCommand", "user-1", "key")]
        public async Task DifferentCallerOrOperation_ReadsDifferentRecordKey(string operation, string callerId,
            string key)
        {
            var otherKey = "idempotent-record:" + new IdempotencyScope(operation, callerId, key).ToCacheKeyFragment();
            Assert.NotEqual(RecordKey, otherKey);

            var cache = new Mock<IRemoteCacheService>(MockBehavior.Strict);
            cache
                .Setup(c => c.GetStringAsync(otherKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?) null);

            var service = CreateService(cache.Object);

            var result = await service.GetRecordAsync<SampleRecord>(new IdempotencyScope(operation, callerId, key),
                TestContext.Current.CancellationToken);

            Assert.Null(result);
            cache.Verify(c => c.GetStringAsync(otherKey, It.IsAny<CancellationToken>()), Times.Once);
            cache.VerifyNoOtherCalls();
        }

        [Fact]
        public void SeparatorInsidePart_DoesNotCollideWithOtherSplit()
        {
            var first = new IdempotencyScope("Op", "a:b", "c").ToCacheKeyFragment();
            var second = new IdempotencyScope("Op", "a", "b:c").ToCacheKeyFragment();

            Assert.NotEqual(first, second);
        }
    }

    private sealed record SampleRecord(string Name, int Count);
}