using Moq;
using RedShirt.Example.Api.Common.Redis.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.UnitTests.Tests.Services;

public class RedisLockServiceTests
{
    public class GetLockAsync
    {
        [Fact]
        public async Task Acquires_WhenStringSetSucceeds()
        {
            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(true);
            database
                .Setup(d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns(RedisResult.Create(1));

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);

            var acquired = await service.GetLockAsync("resource", TestContext.Current.CancellationToken);

            Assert.True(acquired.IsAcquired);
            database.Verify(d => d.StringSetAsync(
                (RedisKey) "resource",
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists,
                CommandFlags.DemandMaster), Times.Once);
            acquired.Unlock();
        }

        [Fact]
        public async Task CachesConnection_AcrossCalls()
        {
            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(true);
            database
                .Setup(d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns(RedisResult.Create(1));

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);

            var first = await service.GetLockAsync("resource-a", TestContext.Current.CancellationToken);
            var second = await service.GetLockAsync("resource-b", TestContext.Current.CancellationToken);

            Assert.True(first.IsAcquired);
            Assert.True(second.IsAcquired);
            connectionService.Verify(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            connectionService.VerifyNoOtherCalls();

            first.Unlock();
            second.Unlock();
        }

        [Fact]
        public async Task DoesNotAcquire_WhenStringSetFails()
        {
            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(false);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);

            var acquired = await service.GetLockAsync("resource", TestContext.Current.CancellationToken);

            Assert.False(acquired.IsAcquired);
            acquired.Unlock();
        }

        [Fact]
        public async Task ForwardsCancellationToken_ToConnectionService()
        {
            using var cts = new CancellationTokenSource();

            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(true);
            database
                .Setup(d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns(RedisResult.Create(1));

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(cts.Token))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);

            var acquired = await service.GetLockAsync("resource", cts.Token);

            Assert.True(acquired.IsAcquired);
            connectionService.Verify(s => s.GetConnectionAsync(cts.Token), Times.Once);
            connectionService.VerifyNoOtherCalls();
            acquired.Unlock();
        }
    }

    public class Unlock
    {
        [Fact]
        public async Task IsSafe_WhenLockWasNotAcquired()
        {
            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(false);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);
            var acquired = await service.GetLockAsync("resource", TestContext.Current.CancellationToken);

            var exception = Record.Exception(acquired.Unlock);

            Assert.Null(exception);
            Assert.False(acquired.IsAcquired);
            database.Verify(
                d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()),
                Times.Never);
        }

        [Fact]
        public async Task Releases_WhenLockWasAcquired()
        {
            var database = new Mock<IDatabase>(MockBehavior.Loose);
            database.Setup(d => d.IsConnected(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    CommandFlags.DemandMaster))
                .ReturnsAsync(true);
            database
                .Setup(d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns(RedisResult.Create(1));

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisLockService(connectionService.Object);
            var acquired = await service.GetLockAsync("resource", TestContext.Current.CancellationToken);

            Assert.True(acquired.IsAcquired);
            acquired.Unlock();

            database.Verify(
                d => d.ScriptEvaluate(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()),
                Times.AtLeastOnce);
        }
    }
}