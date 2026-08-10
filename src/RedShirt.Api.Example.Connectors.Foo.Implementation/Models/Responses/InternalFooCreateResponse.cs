namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Responses;

/// <summary>
///     Response body shape returned by the Foo HTTP API.
/// </summary>
internal sealed class InternalFooCreateResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}