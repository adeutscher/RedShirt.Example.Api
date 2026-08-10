using RedShirt.Api.Example.Connectors.Foo.Core.Models;

namespace RedShirt.Api.Example.Connectors.Foo.Core.Services;

/// <summary>
///     Opaque connector for the Foo dependency.
///     Foo is a representation of an arbitrary external service for this API template.
/// </summary>
public interface IFooConnector
{
    Task<CreateFooConnectorResponse> CreateAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<GetFooConnectorResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}