using RedShirt.Example.Api.Common.Distributed.Models;

namespace RedShirt.Example.Api.Common.Distributed.Services.Abstractions;

public interface IAbstractedLockService
{
    TimeSpan Timeout { get; }
    Task<IAbstractedLock> GetLockAsync(string lockName, CancellationToken cancellationToken = default);
}