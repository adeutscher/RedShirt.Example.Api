namespace RedShirt.Example.Api.Common.Distributed.Models;

public interface IAbstractedLock
{
    bool IsAcquired { get; }
    Task UnlockAsync(CancellationToken cancellationToken = default);
}