using RedShirt.Api.Example.Connectors.Foo.Core.Models;
using RedShirt.Api.Example.Connectors.Foo.Core.Services;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Factories;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Services;

/// <summary>
///     Foo connector implementation: maps Core requests onto the Foo HTTP client under the retry wrapper.
/// </summary>
internal sealed class FooConnector(
    IFooApiClientFactory fooApiClientFactory,
    IFooRetryWrapperService retryWrapperService) : IFooConnector
{
    public Task<CreateFooConnectorResponse> CreateAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return retryWrapperService.RunAsync(async token =>
        {
            var client = await fooApiClientFactory.CreateFooApiClientAsync(token);
            return await client.CreateFooAsync(request, token);
        }, cancellationToken);
    }

    public Task<GetFooConnectorResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return retryWrapperService.RunAsync(async token =>
        {
            var client = await fooApiClientFactory.CreateFooApiClientAsync(token);
            return await client.GetFooByIdAsync(id, token);
        }, cancellationToken);
    }
}