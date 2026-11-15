using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.RemoveUnverifiedCopy;

public class RemoveUnverifiedUploadCopyCommandValidator : AbstractValidator<RemoveUnverifiedUploadCopyCommand>
{
    public RemoveUnverifiedUploadCopyCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
    }
}