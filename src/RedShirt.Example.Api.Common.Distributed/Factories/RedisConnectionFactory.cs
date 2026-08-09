using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Distributed.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using StackExchange.Redis;

namespace RedShirt.Example.Api.Common.Distributed.Factories;

internal interface IRedisConnectionFactory
{
    Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default);
}

internal sealed class RedisConnectionFactory(
    ISecretManagerCacheService secretManager,
    IOptions<RedisConnectionFactory.ConfigurationModel> options) : IRedisConnectionFactory
{
    public async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ConnectionMultiplexer.ConnectAsync(
                await secretManager.GetSecretAsync(options.Value.ConnectionStringPath,
                    cancellationToken: cancellationToken), opts =>
                {
                    opts.ConnectTimeout = 2000;
                    opts.ConnectRetry = 0;
                });
        }
        catch (ApiSecretManagerException e)
        {
            throw new ApiDistributedException(e)
            {
                CouldBeTransient = e.CouldBeTransient,
                IsHandled = e.IsHandled,
                CouldBeExternallySolvable = e.CouldBeExternallySolvable
            };
        }
    }

    public sealed class ConfigurationModel
    {
        public required string ConnectionStringPath { get; init; }
    }
}