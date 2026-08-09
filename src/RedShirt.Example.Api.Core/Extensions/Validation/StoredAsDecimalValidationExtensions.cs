using FluentValidation;
using System.Globalization;

namespace RedShirt.Example.Api.Core.Extensions.Validation;

/// <summary>
///     FluentValidation helpers for string values that map to decimal storage
///     (DTO properties marked with <c>[StoredAsDecimal]</c>).
/// </summary>
internal static class StoredAsDecimalValidationExtensions
{
    private static bool IsValidStoredDecimal(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    public static void MustBeValidStoredDecimal<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        ruleBuilder
            .Must(IsValidStoredDecimal)
            .WithMessage("'{PropertyName}' must be a valid decimal number");
    }

    public static void MustBeValidStoredDecimalWhenPresent<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        ruleBuilder
            .Must(value => string.IsNullOrWhiteSpace(value) || IsValidStoredDecimal(value))
            .WithMessage("'{PropertyName}' must be a valid decimal number");
    }
}
