namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Models.Responses;

/// <summary>
///     Response body shape returned by the Bar HTTP API.
/// </summary>
internal sealed class InternalBarCreateResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}