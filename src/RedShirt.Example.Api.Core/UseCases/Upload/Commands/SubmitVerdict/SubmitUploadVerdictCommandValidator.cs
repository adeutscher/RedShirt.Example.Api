using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;

public class SubmitUploadVerdictCommandValidator : AbstractValidator<SubmitUploadVerdictCommand>
{
    public SubmitUploadVerdictCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
    }
}