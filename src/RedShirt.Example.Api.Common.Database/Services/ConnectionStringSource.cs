using Microsoft.Extensions.Configuration;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.Database.Services;

public interface IConnectionStringSource
{
    Task<string> GetConnectionStringAsync(string name, CancellationToken cancellationToken = default);
}

public class ConnectionStringSource(
    ISecretManagerCacheService secretManagerCacheService,
    IConfigurationRoot configurationRoot) : IConnectionStringSource
{
    public async Task<string> GetConnectionStringAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = configurationRoot.GetConnectionString(name);
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"No connection string found for '{name}'.");
        }

        var result = await secretManagerCacheService.GetSecretAsync(path, cancellationToken: cancellationToken);
        return result.Value;
    }
}