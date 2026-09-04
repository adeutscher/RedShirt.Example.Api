namespace RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

/// <summary>
///     Marks a string property that represents a decimal value for OpenAPI/wire format,
///     but should be stored and queried as a database decimal.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class StoredAsDecimalAttribute : Attribute
{
}