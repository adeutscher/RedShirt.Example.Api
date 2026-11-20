using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Authorization.Extensions;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Messages.Commands.Send;
using RedShirt.Example.Api.Core.UseCases.Messages.Queries.Stream;
using RedShirt.Example.Api.Models.Messages;
using RedShirt.Example.Api.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("messages")]
public class MessageController : ControllerBase
{
    private static async IAsyncEnumerable<string> ToServerSentEvents(
        IAsyncEnumerable<ExampleMessageModel> messages,
        [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
        {
            yield return message.Message;
        }
    }

    /// <summary>Event stream</summary>
    /// <remarks>
    ///     This GET /messages/event-stream endpoint is not directly supported by OpenAPI.
    ///     The generated NSwag <c>MessageClient.GetEventStreamAsync</c> method is intended for connectivity checks only.
    ///     Use MessagesEventStreamListener in RedShirt.Example.Api.Interop project to receive message events.
    /// </remarks>
    [HttpGet("event-stream")]
    [ApproveReadOnly]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IResult> GetEventStream(
        [FromServices]
        IStreamExampleMessagesQueryHandler streamExampleMessagesQueryHandler,
        [FromServices]
        IServerSentEventConnectionLimiter connectionLimiter,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return TypedResults.BadRequest("Authenticated user id is required.");
        }

        if (!connectionLimiter.TryAcquire(userId, out var lease))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                detail: "Too many open event streams for this user. Close one before opening another.");
        }

        // Frees the slot when the request ends, however it ends: stream finished, client disconnected, or an exception
        // before streaming started.
        HttpContext.Response.RegisterForDispose(lease);

        var messageStream =
            await streamExampleMessagesQueryHandler.Handle(new StreamExampleMessagesQuery(userId), cancellationToken);

        return TypedResults.ServerSentEvents(
            ToServerSentEvents(messageStream, cancellationToken),
            "message");
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Post(
        [FromBody]
        ExampleMessagePostRequest request,
        [FromServices]
        ISendExampleMessageCommandHandler sendExampleMessageCommandHandler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return BadRequest("Authenticated user id is required.");
        }

        await sendExampleMessageCommandHandler.Handle(new SendExampleMessageCommand(userId, request.Message),
            cancellationToken);

        return Accepted();
    }
}