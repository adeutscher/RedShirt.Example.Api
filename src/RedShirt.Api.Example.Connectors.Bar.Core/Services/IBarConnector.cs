using RedShirt.Api.Example.Connectors.Bar.Core.Models;

namespace RedShirt.Api.Example.Connectors.Bar.Core.Services;

/// <summary>
///     Opaque connector for the Bar dependency.
///     Bar is a representation of an arbitrary external service for this API template.
/// </summary>
public interface IBarConnector
{
    Task<CreateBarConnectorResponse> CreateAsync(CreateBarConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<GetBarConnectorResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}