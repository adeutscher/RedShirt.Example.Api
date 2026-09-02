using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Purge;

public class PurgeUploadCommandValidator : AbstractValidator<PurgeUploadCommand>
{
    public PurgeUploadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
