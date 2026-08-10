using RedShirt.Api.Example.Connectors.Bar.Core.Models;
using RedShirt.Api.Example.Connectors.Bar.Core.Services;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Factories;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Services;

/// <summary>
///     Bar connector implementation: maps Core requests onto the Bar HTTP client under the retry wrapper.
/// </summary>
internal sealed class BarConnector(
    IBarApiClientFactory barApiClientFactory,
    IBarRetryWrapperService retryWrapperService) : IBarConnector
{
    public Task<CreateBarConnectorResponse> CreateAsync(CreateBarConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return retryWrapperService.RunAsync(async token =>
        {
            var client = barApiClientFactory.CreateBarApiClient();
            return await client.CreateBarAsync(request, token);
        }, cancellationToken);
    }

    public Task<GetBarConnectorResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return retryWrapperService.RunAsync(async token =>
        {
            var client = barApiClientFactory.CreateBarApiClient();
            return await client.GetBarByIdAsync(id, token);
        }, cancellationToken);
    }
}