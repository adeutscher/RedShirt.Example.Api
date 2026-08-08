namespace RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;

/// <summary>
///     Identifies a property for which to not generate search parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CannotFilterByAttribute : Attribute
{
}