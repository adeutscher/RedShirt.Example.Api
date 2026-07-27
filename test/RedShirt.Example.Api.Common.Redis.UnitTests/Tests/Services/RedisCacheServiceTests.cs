using Moq;
using RedShirt.Example.Api.Common.Redis.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.UnitTests.Tests.Services;

public class RedisCacheServiceTests
{
    public class GetStringAsync
    {
        [Fact]
        public async Task CachesConnection_AcrossCalls()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue) "value");

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            _ = await service.GetStringAsync("key-a", TestContext.Current.CancellationToken);
            _ = await service.GetStringAsync("key-b", TestContext.Current.CancellationToken);

            connectionService.Verify(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            connectionService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForwardsCancellationToken_ToConnectionService()
        {
            using var cts = new CancellationTokenSource();

            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue) "value");

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(cts.Token))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            _ = await service.GetStringAsync("key", cts.Token);

            connectionService.Verify(s => s.GetConnectionAsync(cts.Token), Times.Once);
            connectionService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReturnsNull_WhenKeyIsMissing()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringGetAsync((RedisKey) "missing", It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            Assert.Null(await service.GetStringAsync("missing", TestContext.Current.CancellationToken));
            database.Verify(d => d.StringGetAsync((RedisKey) "missing", It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task ReturnsValue_FromDatabase()
        {
            const string key = "greeting";
            const string value = "hello";

            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringGetAsync((RedisKey) key, It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue) value);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            Assert.Equal(value, await service.GetStringAsync(key, TestContext.Current.CancellationToken));
            database.Verify(d => d.StringGetAsync((RedisKey) key, It.IsAny<CommandFlags>()), Times.Once);
        }
    }

    public class SetStringAsync
    {
        [Fact]
        public async Task CachesConnection_AcrossCalls()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Expiration>(),
                    It.IsAny<ValueCondition>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            await service.SetStringAsync("key-a", "a", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            await service.SetStringAsync("key-b", "b", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

            connectionService.Verify(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            connectionService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForwardsCancellationToken_ToConnectionService()
        {
            using var cts = new CancellationTokenSource();

            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Expiration>(),
                    It.IsAny<ValueCondition>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(cts.Token))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            await service.SetStringAsync("key", "value", TimeSpan.FromMinutes(1), cts.Token);

            connectionService.Verify(s => s.GetConnectionAsync(cts.Token), Times.Once);
            connectionService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task WritesValueWithExpiration()
        {
            const string key = "greeting";
            const string value = "hello";
            var expiration = TimeSpan.FromMinutes(5);

            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringSetAsync(
                    (RedisKey) key,
                    (RedisValue) value,
                    It.IsAny<Expiration>(),
                    It.IsAny<ValueCondition>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            await service.SetStringAsync(key, value, expiration, TestContext.Current.CancellationToken);

            database.Verify(d => d.StringSetAsync(
                (RedisKey) key,
                (RedisValue) value,
                It.Is<Expiration>(e => e.Equals((Expiration) expiration)),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }
    }

    public class SetThenGet
    {
        [Fact]
        public async Task ReusesCachedConnection()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database
                .Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Expiration>(),
                    It.IsAny<ValueCondition>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            database
                .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue) "hello");

            var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
            connection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var connectionService = new Mock<IRedisSharedConnectionService>(MockBehavior.Strict);
            connectionService
                .Setup(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var service = new RedisCacheService(connectionService.Object);

            await service.SetStringAsync("greeting", "hello", TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken);
            Assert.Equal("hello",
                await service.GetStringAsync("greeting", TestContext.Current.CancellationToken));

            connectionService.Verify(s => s.GetConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            connectionService.VerifyNoOtherCalls();
        }
    }
}