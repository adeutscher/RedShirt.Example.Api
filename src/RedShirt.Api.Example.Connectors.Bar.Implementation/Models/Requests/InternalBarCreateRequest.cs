namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Models.Requests;

/// <summary>
///     Request body shape expected by the Bar HTTP API.
/// </summary>
internal sealed class InternalBarCreateRequest
{
    public required string Name { get; init; }
}