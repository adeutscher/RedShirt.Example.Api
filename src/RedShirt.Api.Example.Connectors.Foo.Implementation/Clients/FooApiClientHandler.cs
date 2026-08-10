using RedShirt.Api.Example.Connectors.Foo.Implementation.Constants;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;
using System.Net;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

/// <summary>
///     Attaches the Foo static API key (resolved from the secret manager) to outbound requests.
///     On <see cref="HttpStatusCode.Unauthorized" />, force-refreshes the key once and retries.
/// </summary>
internal sealed class FooApiClientHandler(
    IFooApiRequestHandlerRetryWrapperService apiRequestRetryWrapperService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await apiRequestRetryWrapperService.ExecuteAsync(async ct =>
        {
            request.Headers.Remove(FooApiHeaderNames.ApiKey);
            request.Headers.TryAddWithoutValidation(FooApiHeaderNames.ApiKey,
                await apiRequestRetryWrapperService.GetApiKeyAsync(ct));

            var response = await base.SendAsync(request, ct);

            // ReSharper disable once InvertIf
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new FooApiRequestHandlerRetryWrapperService.UnauthorizedException();
            }

            return response;
        }, cancellationToken);
    }
}
