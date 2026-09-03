using FluentValidation;
using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.Extensions.Validation;

internal static class Sha256ChecksumValidationExtensions
{
    public static void MustBeValidSha256ChecksumWhenPresent<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        ruleBuilder
            .Must(value => string.IsNullOrWhiteSpace(value) || Sha256ChecksumMethods.IsValid(value))
            .WithMessage(Sha256ChecksumMethods.InvalidMessage);
    }
}
