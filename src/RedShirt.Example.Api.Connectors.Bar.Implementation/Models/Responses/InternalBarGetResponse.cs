namespace RedShirt.Example.Api.Connectors.Bar.Implementation.Models.Responses;

/// <summary>
///     Response body shape returned by the Bar HTTP API for get-by-id.
/// </summary>
internal sealed class InternalBarGetResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}