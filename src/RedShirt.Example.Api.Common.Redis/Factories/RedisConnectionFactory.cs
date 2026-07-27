using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Redis.Factories;

internal interface IRedisConnectionFactory
{
    Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default);
}

internal class RedisConnectionFactory(
    ISecretManagerCacheService secretManager,
    IOptions<RedisConnectionFactory.ConfigurationModel> options) : IRedisConnectionFactory
{
    public async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString =
            await secretManager.GetSecretAsync(options.Value.ConnectionStringPath,
                cancellationToken: cancellationToken);

        return await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    public sealed class ConfigurationModel
    {
        public required string ConnectionStringPath { get; init; }
    }
}