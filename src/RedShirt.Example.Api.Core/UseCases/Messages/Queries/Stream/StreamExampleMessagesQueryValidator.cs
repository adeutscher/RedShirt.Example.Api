using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Messages.Queries.Stream;

public sealed class StreamExampleMessagesQueryValidator : AbstractValidator<StreamExampleMessagesQuery>
{
    public StreamExampleMessagesQueryValidator()
    {
        RuleFor(query => query.UserId)
            .Must(userId => !string.IsNullOrWhiteSpace(userId))
            .WithMessage("UserId is required");
    }
}