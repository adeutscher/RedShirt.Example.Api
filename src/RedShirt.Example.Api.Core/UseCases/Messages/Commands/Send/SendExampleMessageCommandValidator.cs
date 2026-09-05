using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Messages.Commands.Send;

public sealed class SendExampleMessageCommandValidator : AbstractValidator<SendExampleMessageCommand>
{
    public SendExampleMessageCommandValidator()
    {
        RuleFor(command => command.UserId)
            .Must(userId => !string.IsNullOrWhiteSpace(userId))
            .WithMessage("UserId is required");

        RuleFor(command => command.Message)
            .Must(message => !string.IsNullOrWhiteSpace(message))
            .WithMessage("Message is required");
    }
}