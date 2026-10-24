using System.Text;

namespace RedShirt.Example.Api.Scaffolding.Editing;

/// <summary>
///     Reads and writes text with LF line endings in memory while keeping a file's BOM and CRLF endings on disk.
/// </summary>
internal sealed class TextFile
{
    private static readonly UTF8Encoding WithBom = new(true);
    private static readonly UTF8Encoding WithoutBom = new(false);

    private TextFile(string text, bool hasBom, bool usesCrLf)
    {
        Text = text;
        HasBom = hasBom;
        UsesCrLf = usesCrLf;
    }

    /// <summary>
    ///     Content with LF line endings.
    /// </summary>
    public string Text { get; set; }

    private bool HasBom { get; }
    private bool UsesCrLf { get; }

    public static TextFile Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes is [0xEF, 0xBB, 0xBF, ..];
        var text = WithoutBom.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        var usesCrLf = text.Contains("\r\n", StringComparison.Ordinal);
        return new TextFile(text.Replace("\r\n", "\n"), hasBom, usesCrLf);
    }

    public void Write(string path)
    {
        var text = UsesCrLf ? Text.Replace("\n", "\r\n") : Text;
        File.WriteAllText(path, text, HasBom ? WithBom : WithoutBom);
    }

    /// <summary>
    ///     Compare file content with rendered output, ignoring BOM and line-ending differences.
    /// </summary>
    public static bool Matches(string path, string expected)
    {
        return Read(path).Text == expected.Replace("\r\n", "\n");
    }
}
