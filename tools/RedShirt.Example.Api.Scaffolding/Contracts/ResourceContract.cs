using RedShirt.Example.Api.Scaffolding.Utility;

namespace RedShirt.Example.Api.Scaffolding.Contracts;

/// <summary>
///     One property of a data store request, response, or DTO type.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Type">C# type as written in source (e.g. "string?", "Guid").</param>
/// <param name="NonNullableType">Type without a nullable marker (e.g. "string", "Guid").</param>
/// <param name="Namespace">Namespace to import for the type, or "" for System and built-in types.</param>
/// <param name="IsNullable">Whether the property accepts null.</param>
/// <param name="IsReferenceType">Whether the underlying type is a reference type.</param>
/// <param name="IsString">Whether the underlying type is string.</param>
/// <param name="IsArray">Whether the underlying type is an array (e.g. a [RangeSearchable] filter).</param>
/// <param name="SampleValue">A C# expression of the underlying type, for test skeletons.</param>
internal sealed record ContractProperty(
    string Name,
    string Type,
    string NonNullableType,
    string Namespace,
    bool IsNullable,
    bool IsReferenceType,
    bool IsString,
    bool IsArray,
    string SampleValue)
{
    public string CamelName => Naming.ToCamelCase(Name);
}

/// <summary>
///     A type in the contract with its namespace ("" for System and built-in types).
/// </summary>
internal sealed record ContractType(string Name, string Namespace, IReadOnlyList<ContractProperty> Properties);

/// <summary>
///     The data store's service contract, read from <c>I{Resource}Service</c>. Property lists are in declaration
///     order. A null request means the service has no matching method (e.g. no PostAsync under [DoNotGeneratePost]).
/// </summary>
internal sealed record ResourceContract
{
    public required ContractType Service { get; init; }
    public required ContractType Dto { get; init; }
    public required ContractProperty Key { get; init; }
    public required bool HasGet { get; init; }
    public required bool HasDelete { get; init; }
    public required ContractType? PostRequest { get; init; }
    public required ContractType? PutRequest { get; init; }
    public required ContractType? PatchRequest { get; init; }
    public required ContractType? SearchRequest { get; init; }
    public required ContractType? SearchResponse { get; init; }
    public required string? ContinuationTokenType { get; init; }

    public required string RegistrationMethod { get; init; }
    public required string RegistrationNamespace { get; init; }
    public required bool RegistrationTakesConfiguration { get; init; }
}
