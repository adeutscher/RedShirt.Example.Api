using RedShirt.Api.Example.Connectors.Foo.Core.Models;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Requests;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Models.Responses;
using System.Text;
using System.Text.Json;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

internal interface IFooApiClient
{
    Task<CreateFooConnectorResponse> CreateFooAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<GetFooConnectorResponse> GetFooByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
///     HTTP transport for the Foo dependency. Failures surface as raw framework exceptions
///     (<see cref="HttpRequestException" />, <see cref="JsonException" />, timeouts, etc.).
/// </summary>
internal sealed class FooApiClient(HttpClient httpClient, string baseUrl) : IFooApiClient
{
    public async Task<CreateFooConnectorResponse> CreateFooAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl.TrimEnd('/')}/api/foo"));
        message.Content = new StringContent(JsonSerializer.Serialize(new InternalFooCreateRequest
        {
            Name = request.Name
        }), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int) response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObject = JsonSerializer.Deserialize<InternalFooCreateResponse>(stringResponse);
        if (responseObject is null)
        {
            throw new JsonException("Foo API create response body deserialized to null.");
        }

        return new CreateFooConnectorResponse
        {
            Id = responseObject.Id,
            Name = responseObject.Name
        };
    }

    public async Task<GetFooConnectorResponse> GetFooByIdAsync(int id,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get,
            new Uri($"{baseUrl.TrimEnd('/')}/api/foo/{id}"));

        using var response = await httpClient.SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int) response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObject = JsonSerializer.Deserialize<InternalFooGetResponse>(stringResponse);
        if (responseObject is null)
        {
            throw new JsonException("Foo API get response body deserialized to null.");
        }

        return new GetFooConnectorResponse
        {
            Id = responseObject.Id,
            Name = responseObject.Name
        };
    }
}