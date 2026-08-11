using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.Common.Distributed.Exceptions;
using RedShirt.Example.Api.Common.Distributed.Factories;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Models;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Distributed.UnitTests.Tests.Factories;

public class RedisConnectionFactoryTests
{
    [Fact]
    public async Task GetConnectionAsync_ThrowsRedisConnectionException_WhenEndpointIsUnreachable()
    {
        const string connectionStringPath = "redis/connection-string";

        var secrets = new Mock<ISecretManagerCacheService>(MockBehavior.Strict);
        secrets
            .Setup(s => s.GetSecretAsync(connectionStringPath, null, false, TestContext.Current.CancellationToken))
            .ReturnsAsync(new SecretManagerCacheSecretResponse
            {
                Value = "localhost:1",
                QueriedSecretManager = true
            });

        var factory = new RedisConnectionFactory(
            secrets.Object,
            Options.Create(new RedisConnectionFactory.ConfigurationModel
            {
                ConnectionStringPath = connectionStringPath
            }));

        await Assert.ThrowsAsync<RedisConnectionException>(() =>
            factory.GetConnectionAsync(TestContext.Current.CancellationToken));

        secrets.Verify(s => s.GetSecretAsync(connectionStringPath, null, false, TestContext.Current.CancellationToken),
            Times.Once);
        secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task GetConnectionAsync_WrapsSecretManagerExceptionAsApiDistributedException(
        bool isTransient, bool couldBeExternallySolvable)
    {
        const string connectionStringPath = "redis/connection-string";
        var secretException = new ApiSecretManagerException("secret lookup failed")
        {
            CouldBeTransient = isTransient, IsHandled = false, CouldBeExternallySolvable = couldBeExternallySolvable
        };

        var secrets = new Mock<ISecretManagerCacheService>(MockBehavior.Strict);
        secrets
            .Setup(s => s.GetSecretAsync(connectionStringPath, null, false, TestContext.Current.CancellationToken))
            .ThrowsAsync(secretException);

        var factory = new RedisConnectionFactory(
            secrets.Object,
            Options.Create(new RedisConnectionFactory.ConfigurationModel
            {
                ConnectionStringPath = connectionStringPath
            }));

        var thrown = await Assert.ThrowsAsync<ApiDistributedException>(() =>
            factory.GetConnectionAsync(TestContext.Current.CancellationToken));

        Assert.Equal(secretException.Message, thrown.Message);
        Assert.Same(secretException, thrown.InnerException);
        Assert.Equal(isTransient, thrown.CouldBeTransient);
        Assert.False(thrown.IsHandled);
        Assert.Equal(couldBeExternallySolvable, thrown.CouldBeExternallySolvable);
        secrets.Verify(s => s.GetSecretAsync(connectionStringPath, null, false, TestContext.Current.CancellationToken),
            Times.Once);
        secrets.VerifyNoOtherCalls();
    }
}