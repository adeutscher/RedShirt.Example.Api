using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;

public class CreateUploadCommandValidator : AbstractValidator<CreateUploadCommand>
{
    public CreateUploadCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.UploadedByUserId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
    }
}
