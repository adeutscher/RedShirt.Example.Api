using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;

public class DeleteUploadCommandValidator : AbstractValidator<DeleteUploadCommand>
{
    public DeleteUploadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}