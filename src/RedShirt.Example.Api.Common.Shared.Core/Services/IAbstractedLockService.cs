using RedShirt.Example.Api.Common.Shared.Core.Abstractions;

namespace RedShirt.Example.Api.Common.Shared.Core.Services;

public interface IAbstractedLockService
{
    Task<IAbstractedLock> GetLockAsync(string key);
}