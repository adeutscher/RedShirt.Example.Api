namespace RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

/// <summary>
///     Marks a string property that represents a decimal value on the wire and in OpenAPI
///     (<c>type: number</c>, <c>format: decimal</c>), but should be stored and queried as a database decimal.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class StoredAsDecimalAttribute : Attribute
{
}