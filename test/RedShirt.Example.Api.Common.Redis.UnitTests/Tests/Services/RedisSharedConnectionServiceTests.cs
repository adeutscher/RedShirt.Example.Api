using Moq;
using RedShirt.Example.Api.Common.Redis.Factories;
using RedShirt.Example.Api.Common.Redis.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.UnitTests.Tests.Services;

public class RedisSharedConnectionServiceTests
{
    public class GetConnectionAsync
    {
        [Fact]
        public async Task ConcurrentCalls_CreateConnectionOnce()
        {
            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            var factory = new Mock<IRedisConnectionFactory>(MockBehavior.Strict);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            factory
                .Setup(f => f.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken _) =>
                {
                    await gate.Task;
                    return connection.Object;
                });

            var service = new RedisSharedConnectionService(factory.Object);

            var first = service.GetConnectionAsync(TestContext.Current.CancellationToken);
            var second = service.GetConnectionAsync(TestContext.Current.CancellationToken);

            // Give both callers a chance to contend on the semaphore before releasing the factory.
            await Task.Delay(50, TestContext.Current.CancellationToken);
            gate.SetResult();

            var results = await Task.WhenAll(first, second);

            Assert.Same(connection.Object, results[0]);
            Assert.Same(connection.Object, results[1]);
            factory.Verify(f => f.GetConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            factory.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Miss_CallsFactoryOnceAndCachesResult()
        {
            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            var factory = new Mock<IRedisConnectionFactory>(MockBehavior.Strict);
            factory
                .Setup(f => f.GetConnectionAsync(TestContext.Current.CancellationToken))
                .ReturnsAsync(connection.Object);

            var service = new RedisSharedConnectionService(factory.Object);

            var first = await service.GetConnectionAsync(TestContext.Current.CancellationToken);
            var second = await service.GetConnectionAsync(TestContext.Current.CancellationToken);

            Assert.Same(connection.Object, first);
            Assert.Same(first, second);
            factory.Verify(f => f.GetConnectionAsync(TestContext.Current.CancellationToken), Times.Once);
            factory.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task PreCancelledToken_ThrowsBeforeFactoryCall()
        {
            var factory = new Mock<IRedisConnectionFactory>(MockBehavior.Strict);
            var service = new RedisSharedConnectionService(factory.Object);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.GetConnectionAsync(new CancellationToken(true)));

            factory.VerifyNoOtherCalls();
        }
    }
}