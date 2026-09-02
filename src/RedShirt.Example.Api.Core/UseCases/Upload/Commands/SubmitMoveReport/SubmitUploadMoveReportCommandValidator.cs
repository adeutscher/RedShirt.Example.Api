using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;

public class SubmitUploadMoveReportCommandValidator : AbstractValidator<SubmitUploadMoveReportCommand>
{
    public SubmitUploadMoveReportCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
        RuleFor(x => x.VerifiedStorageObjectKey).NotEmpty();
    }
}
