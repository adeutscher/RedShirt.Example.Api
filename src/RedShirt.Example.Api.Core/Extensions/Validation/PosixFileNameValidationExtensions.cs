using FluentValidation;
using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.Extensions.Validation;

internal static class PosixFileNameValidationExtensions
{
    public static void MustBePosixCompliantFileName<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        ruleBuilder
            .Must(PosixFileName.IsValid)
            .WithMessage(PosixFileName.InvalidMessage);
    }

    public static void MustBePosixCompliantFileNameWhenPresent<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        ruleBuilder
            .Must(value => string.IsNullOrWhiteSpace(value) || PosixFileName.IsValid(value))
            .WithMessage(PosixFileName.InvalidMessage);
    }
}
