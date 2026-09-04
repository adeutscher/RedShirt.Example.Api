using System.Text.RegularExpressions;

namespace RedShirt.Example.Api.Upload.Core.Validation;

/// <summary>
///     Validates SHA-256 digests encoded as lowercase or uppercase hexadecimal strings.
/// </summary>
public static partial class Sha256ChecksumMethods
{
    private const int Length = 64;

    public const string InvalidMessage =
        "SHA-256 checksum must be exactly 64 hexadecimal characters.";

    [GeneratedRegex("^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256HexPattern();

    public static bool IsValid(string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum) || checksum.Length != Length)
        {
            return false;
        }

        return Sha256HexPattern().IsMatch(checksum);
    }
}