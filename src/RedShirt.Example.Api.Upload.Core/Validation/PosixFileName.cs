namespace RedShirt.Example.Api.Upload.Core.Validation;

/// <summary>
///     Validates single-segment file names using the POSIX portable filename character set
///     (<c>[A-Za-z0-9._-]</c>).
/// </summary>
public static class PosixFileName
{
    public const int MaxLength = 255;

    public const string InvalidMessage =
        "File name must be a single POSIX portable file name using characters [A-Za-z0-9._-], and cannot be '.' or '..'.";

    public static bool IsValid(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName is "." or "..")
        {
            return false;
        }

        if (fileName.Length > MaxLength)
        {
            return false;
        }

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('\0'))
        {
            return false;
        }

        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var character in fileName)
        {
            if (!IsPortableCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPortableCharacter(char character)
    {
        return character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '.'
            or '_'
            or '-';
    }
}
