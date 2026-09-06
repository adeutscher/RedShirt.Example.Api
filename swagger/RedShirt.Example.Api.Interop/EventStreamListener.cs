using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RedShirt.Example.Api.Interop;

public abstract class EventStreamListener
{
    private static readonly Lazy<JsonSerializerOptions> DefaultJsonSerializerSettings =
        new(CreateJsonSerializerSettings, true);

    private readonly HttpClient _httpClient;

    protected EventStreamListener(string baseUrl, HttpClient httpClient, string eventEndpointPath)
    {
        if (eventEndpointPath == null)
        {
            throw new ArgumentNullException(nameof(eventEndpointPath));
        }

        BaseUrl = NormalizeBaseUrl(baseUrl);
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        EventEndpointPath = NormalizeEventEndpointPath(eventEndpointPath);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected string BaseUrl { get; }

    // ReSharper disable once MemberCanBePrivate.Global
    protected string EventEndpointPath { get; }

    protected virtual string DefaultEventName => "event";

    protected virtual JsonSerializerOptions JsonSerializerSettings => DefaultJsonSerializerSettings.Value;

    protected virtual void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
    }

    protected virtual void ProcessResponse(HttpClient client, HttpResponseMessage response)
    {
    }

    protected Task StreamEventsAsync(
        Action<string> onEvent,
        CancellationToken cancellationToken = default)
    {
        if (onEvent == null)
        {
            throw new ArgumentNullException(nameof(onEvent));
        }

        return StreamEventsAsync(
            (data, _) =>
            {
                onEvent(data);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    protected Task StreamEventsAsync(
        Func<string, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        if (onEvent == null)
        {
            throw new ArgumentNullException(nameof(onEvent));
        }

        return StreamEventsCoreAsync(onEvent, cancellationToken);
    }

    protected abstract Task StreamEventsCoreAsync(
        Func<string, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken);

    protected async Task OpenEventStreamAsync(
        Func<string, CancellationToken, Task> onEvent,
        string? expectedEventName,
        CancellationToken cancellationToken)
    {
        var client = _httpClient;
        var disposeClient = false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri());
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("text/event-stream"));

            PrepareRequest(client, request, request.RequestUri!.ToString());

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var headers = CopyResponseHeaders(response);
            ProcessResponse(client, response);

            var status = (int) response.StatusCode;

            /*
             * The below is organized as a series of if statements and not a switch statement
             * for consistency with the generated NSwag client code.
             */

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (status == 200)
            {
                if (response.Content == null)
                {
                    return;
                }

                await using var stream = await ReadAsStreamAsync(response.Content, cancellationToken)
                    .ConfigureAwait(false);
                await ReadServerSentEventsAsync(
                        stream,
                        onEvent,
                        expectedEventName,
                        DefaultEventName,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (status == 400)
            {
                var objectResponse = await ReadObjectResponseAsync<ProblemDetails>(response, headers, cancellationToken)
                    .ConfigureAwait(false);
                if (objectResponse.Object == null)
                {
                    throw new SwaggerException(
                        "Response was null which was not expected.",
                        status,
                        objectResponse.Text,
                        headers,
                        null);
                }

                throw new SwaggerException<ProblemDetails>(
                    "A server side error occurred.",
                    status,
                    objectResponse.Text,
                    headers,
                    objectResponse.Object,
                    null);
            }

            if (status == 502)
            {
                var responseText = response.Content == null
                    ? string.Empty
                    : await ReadAsStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
                throw new SwaggerException("A server side error occurred.", status, responseText, headers, null);
            }

            var responseData = response.Content == null
                ? null
                : await ReadAsStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
            throw new SwaggerException(
                "The HTTP status code of the response was not expected (" + status + ").",
                status,
                responseData,
                headers,
                null);
        }
        finally
        {
            if (disposeClient)
            {
                client.Dispose();
            }
        }
    }

    private Uri BuildRequestUri()
    {
        var urlBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(BaseUrl))
        {
            urlBuilder.Append(BaseUrl);
        }

        urlBuilder.Append(EventEndpointPath);
        return new Uri(urlBuilder.ToString(), UriKind.RelativeOrAbsolute);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            return baseUrl;
        }

        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
    }

    private static string NormalizeEventEndpointPath(string eventEndpointPath)
    {
        return eventEndpointPath.TrimStart('/');
    }

    private static Dictionary<string, IEnumerable<string>> CopyResponseHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var item in response.Headers)
        {
            headers[item.Key] = item.Value;
        }

        // ReSharper disable once InvertIf
        if (response.Content?.Headers != null)
        {
            foreach (var item in response.Content.Headers)
            {
                headers[item.Key] = item.Value;
            }
        }

        return headers;
    }

    private static async Task ReadServerSentEventsAsync(
        Stream stream,
        Func<string, CancellationToken, Task> onEvent,
        string? expectedEventName,
        string defaultEventName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        string? eventName = null;
        var dataLines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (dataLines.Count > 0)
                {
                    var resolvedEventName = eventName ?? defaultEventName;
                    if (expectedEventName == null ||
                        string.Equals(resolvedEventName, expectedEventName, StringComparison.Ordinal))
                    {
                        await onEvent(string.Join("\n", dataLines), cancellationToken).ConfigureAwait(false);
                    }
                }

                eventName = null;
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line.Substring("event:".Length).Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line.Substring("data:".Length).TrimStart());
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<ObjectResponseResult<T>> ReadObjectResponseAsync<T>(
        HttpResponseMessage response,
        IReadOnlyDictionary<string, IEnumerable<string>> headers,
        CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return new ObjectResponseResult<T>(default!, string.Empty);
        }

        var responseText = await ReadAsStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
        try
        {
            var typedBody = JsonSerializer.Deserialize<T>(responseText, JsonSerializerSettings);
            return new ObjectResponseResult<T>(typedBody!, responseText);
        }
        catch (JsonException exception)
        {
            var message = "Could not deserialize the response body string as " + typeof(T).FullName + ".";
            throw new SwaggerException(message, (int) response.StatusCode, responseText, headers, exception);
        }
    }

    private static JsonSerializerOptions CreateJsonSerializerSettings()
    {
        return new JsonSerializerOptions();
    }

#if NET5_0_OR_GREATER
    private static Task<string> ReadAsStringAsync(HttpContent content, CancellationToken cancellationToken) =>
        content.ReadAsStringAsync(cancellationToken);

    private static Task<Stream> ReadAsStreamAsync(HttpContent content, CancellationToken cancellationToken) =>
        content.ReadAsStreamAsync(cancellationToken);
#else
    // ReSharper disable UnusedParameter.Local
    private static Task<string> ReadAsStringAsync(HttpContent content, CancellationToken cancellationToken)
    {
        return content.ReadAsStringAsync();
    }

    private static Task<Stream> ReadAsStreamAsync(HttpContent content, CancellationToken cancellationToken)
    {
        return content.ReadAsStreamAsync();
    }
    // ReSharper restore UnusedParameter.Local
#endif

    // ReSharper disable once MemberCanBePrivate.Global
    protected readonly struct ObjectResponseResult<T>
    {
        // ReSharper disable once ConvertToPrimaryConstructor
        public ObjectResponseResult(T responseObject, string responseText)
        {
            Object = responseObject;
            Text = responseText;
        }

        public T Object { get; }

        public string Text { get; }
    }
}

public interface IMessagesEventStreamListener
{
    /// <param name="onEvent">Callback method</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <exception cref="SwaggerException">A server side error occurred.</exception>
    Task StreamEventsAsync(
        Action<string> onEvent,
        CancellationToken cancellationToken = default);

    /// <param name="onEvent">Callback method</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <exception cref="SwaggerException">A server side error occurred.</exception>
    Task StreamEventsAsync(
        Func<string, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default);
}

public sealed class MessagesEventStreamListener : EventStreamListener, IMessagesEventStreamListener
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public MessagesEventStreamListener(
        string baseUrl,
        HttpClient httpClient,
        string eventEndpointPath = "/messages/event-stream")
        : base(baseUrl, httpClient, eventEndpointPath)
    {
    }

    protected override string DefaultEventName => "message";

    /// <param name="onEvent">Callback function</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <exception cref="SwaggerException">A server side error occurred.</exception>
    public new Task StreamEventsAsync(
        Action<string> onEvent,
        CancellationToken cancellationToken = default)
    {
        return base.StreamEventsAsync(onEvent, cancellationToken);
    }

    /// <param name="onEvent">Callback function</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <exception cref="SwaggerException">A server side error occurred.</exception>
    public new Task StreamEventsAsync(
        Func<string, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        return base.StreamEventsAsync(onEvent, cancellationToken);
    }

    protected override Task StreamEventsCoreAsync(
        Func<string, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken)
    {
        return OpenEventStreamAsync(onEvent, DefaultEventName, cancellationToken);
    }
}