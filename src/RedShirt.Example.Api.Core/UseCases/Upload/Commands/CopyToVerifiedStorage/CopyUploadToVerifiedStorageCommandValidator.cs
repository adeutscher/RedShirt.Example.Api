using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.CopyToVerifiedStorage;

public class CopyUploadToVerifiedStorageCommandValidator : AbstractValidator<CopyUploadToVerifiedStorageCommand>
{
    public CopyUploadToVerifiedStorageCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
    }
}
