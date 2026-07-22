namespace RedShirt.Example.Api.Common.Shared.Core.Abstractions;

public interface IAbstractedLock
{
    bool IsAcquired { get; }
    void Unlock();
}