using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.Configuration;
using RedShirt.Example.Api.Controllers;
using RedShirt.Example.Api.Core.UseCases.Messages.Queries.Stream;
using RedShirt.Example.Api.ServerSentEvents;
using System.Security.Claims;

namespace RedShirt.Example.Api.UnitTests.Tests.Controllers;

public class MessageControllerTests
{
    private const string UserId = "user-1";

    private readonly ServerSentEventConnectionLimiter _limiter = new(Options.Create(new ServerSentEventsOptions
    {
        MaxConnectionsPerUser = 1
    }));

    private readonly Mock<IStreamExampleMessagesQueryHandler> _streamHandler = new();

    private static (MessageController Controller, CompletionCapturingResponseFeature Response) Controller()
    {
        var response = new CompletionCapturingResponseFeature();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId)], "test"))
        };
        httpContext.Features.Set<IHttpResponseFeature>(response);

        return (new MessageController {ControllerContext = new ControllerContext {HttpContext = httpContext}},
            response);
    }

    private Task<IResult> GetEventStream(MessageController controller)
    {
        return controller.GetEventStream(_streamHandler.Object, _limiter, TestContext.Current.CancellationToken);
    }

    public MessageControllerTests()
    {
        _streamHandler
            .Setup(handler => handler.Handle(It.IsAny<StreamExampleMessagesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AsyncEnumerable.Empty<ExampleMessageModel>());
    }

    [Fact]
    public async Task GetEventStream_AtLimit_Returns429WithoutSubscribing()
    {
        var (first, _) = Controller();
        await GetEventStream(first);
        _streamHandler.Invocations.Clear();
        var (second, _) = Controller();

        var result = await GetEventStream(second);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.StatusCode);
        _streamHandler.Verify(
            handler => handler.Handle(It.IsAny<StreamExampleMessagesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(1, _limiter.GetOpenCount(UserId));
    }

    [Fact]
    public async Task GetEventStream_HandlerThrows_StillReleasesSlotWhenRequestCompletes()
    {
        _streamHandler
            .Setup(handler => handler.Handle(It.IsAny<StreamExampleMessagesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var (controller, response) = Controller();

        await Assert.ThrowsAsync<InvalidOperationException>(() => GetEventStream(controller));
        await response.CompleteAsync();

        Assert.Equal(0, _limiter.GetOpenCount(UserId));
    }

    [Fact]
    public async Task GetEventStream_UnderLimit_StreamsAndReleasesSlotWhenRequestCompletes()
    {
        var (controller, response) = Controller();

        var result = await GetEventStream(controller);

        Assert.IsType<ServerSentEventsResult<string>>(result);
        Assert.Equal(1, _limiter.GetOpenCount(UserId));

        await response.CompleteAsync();

        Assert.Equal(0, _limiter.GetOpenCount(UserId));
    }

    /// <summary>
    ///     The default response feature drops OnCompleted callbacks, and RegisterForDispose relies on them. This one keeps
    ///     them so a test can end the request the way the server does.
    /// </summary>
    private sealed class CompletionCapturingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onCompleted = [];

        public async Task CompleteAsync()
        {
            foreach (var (callback, state) in _onCompleted)
            {
                await callback(state);
            }
        }

        public override void OnCompleted(Func<object, Task> callback, object state)
        {
            _onCompleted.Add((callback, state));
        }
    }
}