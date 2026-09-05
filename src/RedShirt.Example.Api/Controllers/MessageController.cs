using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.Authorization.Extensions;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Messages.Commands.Send;
using RedShirt.Example.Api.Core.UseCases.Messages.Queries.Stream;
using RedShirt.Example.Api.Models.Messages;
using System.Runtime.CompilerServices;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("messages")]
public class MessageController : ControllerBase
{
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

    [HttpGet("event-stream")]
    [ApproveReadOnly]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IResult> GetEventStream(
        [FromServices]
        IStreamExampleMessagesQueryHandler streamExampleMessagesQueryHandler,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return TypedResults.BadRequest("Authenticated user id is required.");
        }

        var messageStream =
            await streamExampleMessagesQueryHandler.Handle(new StreamExampleMessagesQuery(userId), cancellationToken);

        return TypedResults.ServerSentEvents(
            ToServerSentEvents(messageStream, cancellationToken),
            eventType: "message");
    }

    private static async IAsyncEnumerable<string> ToServerSentEvents(
        IAsyncEnumerable<ExampleMessageModel> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
        {
            yield return message.Message;
        }
    }
}
