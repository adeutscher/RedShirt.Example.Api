namespace RedShirt.Example.Api.Common.Services;

public interface IDataCacheService
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    Task SetStringAsync(string key, string value, TimeSpan expiration, CancellationToken cancellationToken = default);
}