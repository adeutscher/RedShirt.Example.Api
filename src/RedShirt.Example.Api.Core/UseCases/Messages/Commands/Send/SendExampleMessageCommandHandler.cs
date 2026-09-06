using RedShirt.Example.Api.ClientEvents.Domains.Example.Models;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Messages.Commands.Send;

public interface ISendExampleMessageCommandHandler : ICqrsHandler<SendExampleMessageCommand>;

internal sealed class SendExampleMessageCommandHandler(
    IExampleMessageSendService exampleMessageSendService,
    ICoreRequestValidator coreRequestValidator) : ISendExampleMessageCommandHandler
{
    public async Task Handle(SendExampleMessageCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await exampleMessageSendService.SendToUserAsync(command.UserId, new ExampleMessageModel
        {
            Message = command.Message
        }, cancellationToken);
    }
}