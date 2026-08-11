namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Models.Responses;

/// <summary>
///     Response body shape returned by the Foo HTTP API for get-by-id.
/// </summary>
internal sealed class InternalFooGetResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}