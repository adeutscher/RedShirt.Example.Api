namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Requests;

/// <summary>
///     Request body shape expected by the Foo HTTP API.
/// </summary>
internal sealed class InternalFooCreateRequest
{
    public required string Name { get; init; }
}