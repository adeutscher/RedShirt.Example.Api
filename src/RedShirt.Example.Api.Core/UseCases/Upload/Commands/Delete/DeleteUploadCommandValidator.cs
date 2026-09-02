using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;

public class DeleteUploadCommandValidator : AbstractValidator<DeleteUploadCommand>
{
    public DeleteUploadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
