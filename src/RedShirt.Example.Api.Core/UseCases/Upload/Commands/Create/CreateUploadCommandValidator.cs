using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;

public class CreateUploadCommandValidator : AbstractValidator<CreateUploadCommand>
{
    public CreateUploadCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MustBePosixCompliantFileName();
        RuleFor(x => x.UploadedByUserId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.ContentLength).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}