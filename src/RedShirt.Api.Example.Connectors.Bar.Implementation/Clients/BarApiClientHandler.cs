using RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Constants;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;
using System.Net;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Clients;

/// <summary>
///     Attaches the Bar static API key (resolved from the secret manager) to outbound requests.
///     On <see cref="HttpStatusCode.Unauthorized" />, signals <see cref="BarUnauthorizedException" />
///     so the request handler retry wrapper can force-refresh the key and retry once.
/// </summary>
internal sealed class BarApiClientHandler(
    IBarApiRequestHandlerRetryWrapperService apiRequestRetryWrapperService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await apiRequestRetryWrapperService.ExecuteAsync(async ct =>
        {
            request.Headers.Remove(BarApiHeaderNames.ApiKey);
            request.Headers.TryAddWithoutValidation(BarApiHeaderNames.ApiKey,
                await apiRequestRetryWrapperService.GetApiKeyAsync(ct));

            var response = await base.SendAsync(request, ct);

            // ReSharper disable once InvertIf
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new BarUnauthorizedException();
            }

            return response;
        }, cancellationToken);
    }
}