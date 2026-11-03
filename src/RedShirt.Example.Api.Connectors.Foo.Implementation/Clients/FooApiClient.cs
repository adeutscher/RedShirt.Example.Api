using RedShirt.Example.Api.Connectors.Common.Http.Extensions;
using RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;
using RedShirt.Example.Api.Connectors.Foo.Core.Models;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Models.Requests;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Models.Responses;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Clients;

internal interface IFooApiClient
{
    Task<CreateFooConnectorResponse> CreateFooAsync(CreateFooConnectorRequest request,
        CancellationToken cancellationToken = default);

    Task<GetFooConnectorResponse> GetFooByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
///     HTTP transport for the Foo dependency. Failures surface as raw framework exceptions
///     (<see cref="HttpRequestException" />, <see cref="JsonException" />, timeouts, etc.),
///     except get-by-id HTTP 404 which surfaces as <see cref="FooRecordNotFoundException" />, and HTTP 429 or 503
///     which surface as an unhandled <see cref="FooConnectorException" /> carrying the dependency's retry hint.
/// </summary>
internal sealed class FooApiClient(HttpClient httpClient, string baseUrl) : IFooApiClient
{
    private static Exception CreateFailureException(HttpResponseMessage response)
    {
        var exception = new HttpRequestException(
            $"Response status code does not indicate success: {(int) response.StatusCode} ({response.StatusCode}).",
            null,
            response.StatusCode);

        if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable))
        {
            return exception;
        }

        // Not yet handled, so the connector retry wrapper still retries it and can honour the hint.
        return new FooConnectorException(exception)
        {
            IsHandled = false,
            CouldBeTransient = true,
            CouldBeExternallySolvable = true,
            IsThrottled = response.StatusCode == HttpStatusCode.TooManyRequests,
            RetryAfter = response.GetRetryAfter(DateTimeOffset.UtcNow)
        };
    }

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
            throw CreateFailureException(response);
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

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FooRecordNotFoundException(id);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw CreateFailureException(response);
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