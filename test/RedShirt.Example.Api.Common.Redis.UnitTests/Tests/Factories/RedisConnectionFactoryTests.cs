using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.Redis.Factories;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.UnitTests.Tests.Factories;

public class RedisConnectionFactoryTests
{
    [Fact]
    public async Task TestGetConnection()
    {
        const string key = "key";
        // Can't directly connect to redis in a unit test, so giving it a guaranteed-to-fail connection string
        const string connectionString = "localhost:1";

        var secrets = new Mock<ISecretManagerCacheService>(MockBehavior.Strict);
        secrets
            .Setup(s => s.GetSecretAsync(key, It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(connectionString);

        var factory = new RedisConnectionFactory(secrets.Object, Options.Create(
            new RedisConnectionFactory.ConfigurationModel
            {
                ConnectionStringPath = key
            }));

        // Test as far as we can 
        await Assert.ThrowsAsync<RedisConnectionException>(async () =>
            await factory.GetConnectionAsync(TestContext.Current.CancellationToken));

        Assert.Single(secrets.Invocations);
        secrets.Verify(
            s => s.GetSecretAsync(key, It.IsAny<TimeSpan?>(), It.IsAny<bool>(), TestContext.Current.CancellationToken),
            Times.Once);
    }
}