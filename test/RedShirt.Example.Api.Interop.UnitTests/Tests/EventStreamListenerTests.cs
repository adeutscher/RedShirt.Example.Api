using Moq;
using System.Net;
using System.Text;

namespace RedShirt.Example.Api.Interop.UnitTests.Tests;

public class EventStreamListenerTests
{
    private const string BaseUrl = "https://api.example.test/";

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
    public void Constructor_ThrowsArgumentNullException_WhenEventEndpointPathIsNull()
    {
        using var httpClient = new HttpClient();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TestEventStreamListener(BaseUrl, httpClient, null!));

        Assert.Equal("eventEndpointPath", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TestEventStreamListener(BaseUrl, null!, "events/stream"));

        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public async Task StreamEventsAsync_ActionOverload_DeliversEventData()
    {
        var handler = new StubHandler(_ => SseOkResponse(SseBuilder.Event("message", "hello")));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream")
        {
            ExpectedEventName = "message"
        };
        var received = new List<string>();

        await listener.StreamEventsAsyncAction(received.Add, TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_CompletesWithoutCallbacks_WhenResponseContentIsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");
        var callbackCount = 0;

        await listener.StreamAsync(
            (_, _) =>
            {
                callbackCount++;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public async Task StreamEventsAsync_FiltersEvents_WhenExpectedEventNameIsSet()
    {
        var sseBody = SseBuilder.Events(
            ("other", "ignored"),
            ("message", "kept"));
        var handler = new StubHandler(_ => SseOkResponse(sseBody));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream")
        {
            ExpectedEventName = "message"
        };
        var received = new List<string>();

        await listener.StreamAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["kept"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_FuncOverload_PassesCancellationTokenToCallback()
    {
        var handler = new StubHandler(_ => SseOkResponse(SseBuilder.Data("payload")));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");
        CancellationToken? callbackToken = null;

        await listener.StreamAsync(
            (_, cancellationToken) =>
            {
                callbackToken = cancellationToken;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(TestContext.Current.CancellationToken, callbackToken);
    }

    [Fact]
    public async Task StreamEventsAsync_IgnoresCommentLines()
    {
        var sseBody = ": keep-alive\n\n" + SseBuilder.Data("hello");
        var handler = new StubHandler(_ => SseOkResponse(sseBody));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");
        var received = new List<string>();

        await listener.StreamAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["hello"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_InvokesPrepareRequestAndProcessResponseHooks()
    {
        var prepareRequest = new Mock<Action<HttpClient, HttpRequestMessage, string>>();
        var processResponse = new Mock<Action<HttpClient, HttpResponseMessage>>();
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream")
        {
            PrepareRequestHook = prepareRequest.Object,
            ProcessResponseHook = processResponse.Object
        };

        await listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        prepareRequest.Verify(
            hook => hook(httpClient, handler.LastRequest!, handler.LastRequest!.RequestUri!.ToString()),
            Times.Once);
        processResponse.Verify(
            hook => hook(httpClient, It.Is<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.OK)),
            Times.Once);
    }

    [Fact]
    public async Task StreamEventsAsync_NormalizesBaseUrlAndEventEndpointPath()
    {
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener("https://api.example.test", httpClient, "/messages/event-stream");

        await listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.example.test/messages/event-stream",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task StreamEventsAsync_ParsesMultipleEventsAndMultilineData()
    {
        var sseBody =
            "event: message\ndata: line-one\ndata: line-two\n\n" +
            SseBuilder.Event("message", "second");
        var handler = new StubHandler(_ => SseOkResponse(sseBody));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream")
        {
            ExpectedEventName = "message"
        };
        var received = new List<string>();

        await listener.StreamAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["line-one\nline-two", "second"], received);
    }

    [Fact]
    public async Task StreamEventsAsync_PropagatesCancellation_BeforeRequestIsSent()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            listener.StreamAsync((_, _) => Task.CompletedTask, cancellationTokenSource.Token));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task StreamEventsAsync_SendsGetRequest_WithExpectedUrlAndAcceptHeader()
    {
        var handler = new StubHandler(_ => EmptyOkResponse());
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "/messages/event-stream");

        await listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(
            "https://api.example.test/messages/event-stream",
            handler.LastRequest.RequestUri!.ToString());
        Assert.Contains(
            handler.LastRequest.Headers.Accept,
            value => value.MediaType == "text/event-stream");
    }

    [Fact]
    public async Task StreamEventsAsync_ThrowsArgumentNullException_WhenActionCallbackIsNull()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => EmptyOkResponse()));
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            listener.StreamEventsAsyncAction(null!, TestContext.Current.CancellationToken));

        Assert.Equal("onEvent", exception.ParamName);
    }

    [Fact]
    public async Task StreamEventsAsync_ThrowsArgumentNullException_WhenFuncCallbackIsNull()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => EmptyOkResponse()));
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            listener.StreamAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("onEvent", exception.ParamName);
    }

    [Fact]
    public async Task StreamEventsAsync_ThrowsSwaggerExceptionWithProblemDetails_On400()
    {
        const string problemJson = """
                                   {
                                     "title": "Bad Request",
                                     "status": 400,
                                     "detail": "Authenticated user id is required."
                                   }
                                   """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        var exception = await Assert.ThrowsAsync<SwaggerException<ProblemDetails>>(() =>
            listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Bad Request", exception.Result.Title);
        Assert.Equal("Authenticated user id is required.", exception.Result.Detail);
    }

    [Fact]
    public async Task StreamEventsAsync_ThrowsSwaggerException_On502()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream unavailable", Encoding.UTF8, "text/plain")
        });
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        var exception = await Assert.ThrowsAsync<SwaggerException>(() =>
            listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));

        Assert.Equal(502, exception.StatusCode);
        Assert.Equal("upstream unavailable", exception.Response);
    }

    [Fact]
    public async Task StreamEventsAsync_ThrowsSwaggerException_OnUnexpectedStatusCode()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("missing", Encoding.UTF8, "text/plain")
        });
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream");

        var exception = await Assert.ThrowsAsync<SwaggerException>(() =>
            listener.StreamAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken));

        Assert.Equal(404, exception.StatusCode);
        Assert.Contains("was not expected (404)", exception.Message);
        Assert.Equal("missing", exception.Response);
    }

    [Fact]
    public async Task StreamEventsAsync_UsesDefaultEventName_WhenEventLineIsMissing()
    {
        var handler = new StubHandler(_ => SseOkResponse(SseBuilder.Data("implicit-message")));
        using var httpClient = new HttpClient(handler);
        var listener = new TestEventStreamListener(BaseUrl, httpClient, "events/stream")
        {
            ExpectedEventName = "event"
        };
        var received = new List<string>();

        await listener.StreamAsync(
            (data, _) =>
            {
                received.Add(data);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["implicit-message"], received);
    }

    private sealed class TestEventStreamListener : EventStreamListener
    {
        public string? ExpectedEventName { get; init; }

        public Action<HttpClient, HttpRequestMessage, string>? PrepareRequestHook { get; init; }

        public Action<HttpClient, HttpResponseMessage>? ProcessResponseHook { get; init; }

        public TestEventStreamListener(string baseUrl, HttpClient httpClient, string eventEndpointPath)
            : base(baseUrl, httpClient, eventEndpointPath)
        {
        }

        protected override void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
        {
            PrepareRequestHook?.Invoke(client, request, url);
        }

        protected override void ProcessResponse(HttpClient client, HttpResponseMessage response)
        {
            ProcessResponseHook?.Invoke(client, response);
        }

        public Task StreamAsync(
            Func<string, CancellationToken, Task> onEvent,
            CancellationToken cancellationToken = default)
        {
            return StreamEventsAsync(onEvent, cancellationToken);
        }

        public Task StreamEventsAsyncAction(
            Action<string> onEvent,
            CancellationToken cancellationToken = default)
        {
            return StreamEventsAsync(onEvent, cancellationToken);
        }

        protected override Task StreamEventsCoreAsync(
            Func<string, CancellationToken, Task> onEvent,
            CancellationToken cancellationToken)
        {
            return OpenEventStreamAsync(onEvent, ExpectedEventName, cancellationToken);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}