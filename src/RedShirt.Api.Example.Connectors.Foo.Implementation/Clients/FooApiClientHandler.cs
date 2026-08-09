using Microsoft.Extensions.Options;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;
using System.Net;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

/// <summary>
///     Attaches the Foo static API key (resolved from the secret manager) to outbound requests.
///     On <see cref="HttpStatusCode.Unauthorized" />, force-refreshes the key once and retries.
/// </summary>
internal sealed class FooApiClientHandler(
    IFooApiRequestHandlerRetryPolicySource retryPolicySource) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await retryPolicySource.GetApiKeyAsync(cancellationToken);

        return await retryPolicySource.GetRetryPipeline().ExecuteAsync(async token =>
        {
            request.Headers.Remove("x-api-key");
            request.Headers.TryAddWithoutValidation("x-api-key", await retryPolicySource.GetApiKeyAsync(token));

            var response = await base.SendAsync(request, token);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new FooApiRequestHandlerRetryPolicySource.UnauthorizedException();
            }

            return response;
        }, cancellationToken);
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Secret-manager path for the Foo API key (same pattern as connection-string paths).
        /// </summary>
        public required string ApiKeyPath { get; init; }
    }
}
