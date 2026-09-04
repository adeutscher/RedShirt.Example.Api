namespace RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

/// <summary>
///     Suggests a maximum page size for generated code repositories searching this resource.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DbMaxPageSizeAttribute : Attribute
{
    public uint MaxPageSize { get; private set; }

    public DbMaxPageSizeAttribute(uint maxPageSize)
    {
        MaxPageSize = maxPageSize;
    }
}