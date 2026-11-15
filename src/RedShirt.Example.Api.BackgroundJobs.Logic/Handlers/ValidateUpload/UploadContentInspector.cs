using System.Text;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;

/// <summary>
///     The template's stand-in for real validation: a valid document is a text document containing the word "potato".
///     An applied project replaces this with its own checks.
/// </summary>
internal static class UploadContentInspector
{
    internal const string RequiredWord = "potato";

    private const int BufferSize = 8192;

    /// <summary>
    ///     Reads <paramref name="content" /> in chunks rather than all at once, carrying the tail of each chunk over so
    ///     that a word split across two chunks is still found.
    /// </summary>
    internal static async Task<bool> IsValidAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, true, BufferSize, true);
        var buffer = new char[BufferSize];
        var carry = string.Empty;

        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            var window = string.Concat(carry, new string(buffer, 0, read));
            if (window.Contains(RequiredWord, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var carryLength = Math.Min(RequiredWord.Length - 1, window.Length);
            carry = window[^carryLength..];
        }

        return false;
    }
}