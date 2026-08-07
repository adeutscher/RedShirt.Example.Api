namespace RedShirt.Example.Api.Common.Distributed.Models.Safety;

public sealed class SafeDistributedLockOperationResponse : SafeDistributedOperationResponse
{
    public required IAbstractedLock Lock { get; init; }
}