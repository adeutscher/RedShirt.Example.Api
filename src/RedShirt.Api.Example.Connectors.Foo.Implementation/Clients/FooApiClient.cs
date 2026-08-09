using RedShirt.Api.Example.Connectors.Foo.Implementation.Exceptions;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Requests;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Responses;
using System.Text.Json;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

internal interface IFooApiClient
{
    Task<FooApiCreateResponse> CreateFooAsync(FooApiCreateRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     HTTP transport for the Foo dependency. Failures are surfaced as <see cref="ApiFooConnectorException" />.
/// </summary>
internal sealed class FooApiClient(HttpClient httpClient, string baseUrl) : IFooApiClient
{
    public async Task<FooApiCreateResponse> CreateFooAsync(FooApiCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl.TrimEnd('/')}/api/foo"));
            message.Content = new StringContent(JsonSerializer.Serialize(new InternalFooCreateRequest
            {
                Name = request.Name
            }), System.Text.Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiFooConnectorException((int) response.StatusCode);
            }

            var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var responseObject = JsonSerializer.Deserialize<InternalFooCreateResponse>(stringResponse);
                if (responseObject is null)
                {
                    throw new ApiFooConnectorException((int) response.StatusCode);
                }

                return new FooApiCreateResponse
                {
                    Id = responseObject.Id,
                    Name = responseObject.Name
                };
            }
            catch (JsonException ex)
            {
                throw new ApiFooConnectorException((int) response.StatusCode, ex);
            }
        }
        catch (ApiFooConnectorException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiFooConnectorException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiFooConnectorException(ex);
        }
    }
}
