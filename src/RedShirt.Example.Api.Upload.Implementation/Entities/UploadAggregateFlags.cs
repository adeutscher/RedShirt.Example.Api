namespace RedShirt.Example.Api.Upload.Implementation.Entities;

[Flags]
internal enum UploadAggregateFlags
{
    None = 0,
    IsValidated = 1,
    IsRejected = 2
}

internal static class UploadAggregateFlagMapping
{
    public static UploadAggregateFlags FromBoolValues(bool isValidated, bool isRejected)
    {
        var flags = UploadAggregateFlags.None;
        if (isValidated)
        {
            flags |= UploadAggregateFlags.IsValidated;
        }

        if (isRejected)
        {
            flags |= UploadAggregateFlags.IsRejected;
        }

        return flags;
    }

    public static bool HasValidated(UploadAggregateFlags flags)
    {
        return (flags & UploadAggregateFlags.IsValidated) == UploadAggregateFlags.IsValidated;
    }

    public static bool HasRejected(UploadAggregateFlags flags)
    {
        return (flags & UploadAggregateFlags.IsRejected) == UploadAggregateFlags.IsRejected;
    }
}
