namespace RedShirt.Example.Api.Common.Abstractions;

public interface IAbstractedLock
{
    bool IsAcquired { get; }
    void Unlock();
}