namespace RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;

/// <summary>
///     Signals that the code generator should not generate POST methods for this class.
///     Suggests that the attribute user is a DTO is for a supporting attribute whose primary key refers to the primary key
///     of another object.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DoNotGeneratePostAttribute : Attribute
{
}