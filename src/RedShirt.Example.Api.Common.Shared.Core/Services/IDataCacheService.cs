namespace RedShirt.Example.Api.Common.Shared.Core.Services;

public interface IDataCacheService
{
    Task<string?> GetStringAsync(string key);
    Task SetStringAsync(string key, string value, TimeSpan expiration);
}