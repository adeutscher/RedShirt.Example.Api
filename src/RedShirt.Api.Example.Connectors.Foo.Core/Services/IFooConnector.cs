using RedShirt.Api.Example.Connectors.Foo.Core.Models;

namespace RedShirt.Api.Example.Connectors.Foo.Core.Services;

/// <summary>
///     Opaque connector for the Foo dependency. Callers depend on this abstraction rather than HTTP or other transport.
/// </summary>
public interface IFooConnector
{
    Task<CreateFooConnectorResponse> CreateAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default);
}
