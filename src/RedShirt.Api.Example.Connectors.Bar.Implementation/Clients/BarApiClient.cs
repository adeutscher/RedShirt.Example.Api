using RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;
using RedShirt.Api.Example.Connectors.Bar.Core.Models;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Models.Requests;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Models.Responses;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Clients;

internal interface IBarApiClient
{
    Task<CreateBarConnectorResponse> CreateBarAsync(CreateBarConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<GetBarConnectorResponse> GetBarByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
///     HTTP transport for the Bar dependency. Failures surface as raw framework exceptions
///     (<see cref="HttpRequestException" />, <see cref="JsonException" />, timeouts, etc.),
///     except get-by-id HTTP 404 which surfaces as <see cref="BarRecordNotFoundException" />.
/// </summary>
internal sealed class BarApiClient(HttpClient httpClient, string baseUrl) : IBarApiClient
{
    public async Task<CreateBarConnectorResponse> CreateBarAsync(CreateBarConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl.TrimEnd('/')}/api/bar"));
        message.Content = new StringContent(JsonSerializer.Serialize(new InternalBarCreateRequest
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
        var responseObject = JsonSerializer.Deserialize<InternalBarCreateResponse>(stringResponse);
        if (responseObject is null)
        {
            throw new JsonException("Bar API create response body deserialized to null.");
        }

        return new CreateBarConnectorResponse
        {
            Id = responseObject.Id,
            Name = responseObject.Name
        };
    }

    public async Task<GetBarConnectorResponse> GetBarByIdAsync(int id,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get,
            new Uri($"{baseUrl.TrimEnd('/')}/api/bar/{id}"));

        using var response = await httpClient.SendAsync(message, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new BarRecordNotFoundException(id);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int) response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObject = JsonSerializer.Deserialize<InternalBarGetResponse>(stringResponse);
        if (responseObject is null)
        {
            throw new JsonException("Bar API get response body deserialized to null.");
        }

        return new GetBarConnectorResponse
        {
            Id = responseObject.Id,
            Name = responseObject.Name
        };
    }
}