using RedShirt.Example.Api.Common.Abstractions;

namespace RedShirt.Example.Api.Common.Services;

public interface IAbstractedLockService
{
    Task<IAbstractedLock> GetLockAsync(string key, CancellationToken cancellationToken = default);
}