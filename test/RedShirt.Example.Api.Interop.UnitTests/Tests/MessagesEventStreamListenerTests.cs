using System.Net;
using System.Text;

namespace RedShirt.Example.Api.Interop.UnitTests.Tests;

public class MessagesEventStreamListenerTests
{
    private static HttpResponseMessage EmptyOkResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "text/event-stream")
        };
    }

    private static HttpResponseMessage SseOkResponse(string sseBody)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream")
        };
    }

    [Fact]
    public async Task StreamEventsAsync_ActionOverload_DeliversMessageEvents()
    {
        var handler = new StubHandler(_ => SseOkResponse(SseBuilder.Data("hello")));
        using var httpClient = new HttpClient(handler);
        var listener = new MessagesEventStreamListener("https://api.example.test/", httpClient);
        var received = new List<string>();

        await listener.StreamEventsAsync(received.Add, TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_AllowsCustomEventEndpointPath()
    {
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new MessagesEventStreamListener(
            "https://api.example.test/",
            httpClient,
            "/custom/event-stream");

        await listener.StreamEventsAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.example.test/custom/event-stream",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task StreamEventsAsync_CanBeUsedThroughInterface()
    {
        var handler = new StubHandler(_ => SseOkResponse(SseBuilder.Data("via-interface")));
        using var httpClient = new HttpClient(handler);
        IMessagesEventStreamListener listener =
            new MessagesEventStreamListener("https://api.example.test/", httpClient);
        var received = new List<string>();

        await listener.StreamEventsAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["via-interface"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_DeliversOnlyMessageEvents()
    {
        var sseBody = SseBuilder.Events(
            ("heartbeat", "ignored"),
            ("message", "hello"));
        var handler = new StubHandler(_ => SseOkResponse(sseBody));
        using var httpClient = new HttpClient(handler);
        var listener = new MessagesEventStreamListener("https://api.example.test/", httpClient);
        var received = new List<string>();

        await listener.StreamEventsAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_UsesDefaultEventEndpointPath_InRequestUrl()
    {
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new MessagesEventStreamListener("https://api.example.test/", httpClient);

        await listener.StreamEventsAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.example.test/messages/event-stream",
            handler.LastRequest!.RequestUri!.ToString());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}

internal static class SseBuilder
{
    internal static string Data(string data)
    {
        return $"data: {data}\n\n";
    }

    internal static string Event(string eventName, string data)
    {
        return $"event: {eventName}\ndata: {data}\n\n";
    }

    internal static string Events(params (string EventName, string Data)[] events)
    {
        var builder = new StringBuilder();
        foreach (var (eventName, data) in events)
        {
            builder.Append(Event(eventName, data));
        }

        return builder.ToString();
    }
}