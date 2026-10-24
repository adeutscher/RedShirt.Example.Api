using System.Text.RegularExpressions;

namespace RedShirt.Example.Api.Scaffolding.Editing;

internal sealed class AnchorException(string message) : Exception(message);

/// <summary>
///     Inserts and removes snippets above <c>// scaffold:{anchor}</c> comments in shared files.
///     <para>
///         A snippet belongs to the region between its anchor and the anchor line above it (or the start of the file).
///         That keeps identical snippets for different anchors in one file (e.g. two permission lists in the same
///         test class) apart. Snippets are rendered at column 0 and indented to match the anchor.
///     </para>
///     <para>
///         Lines are compared without trailing whitespace or a trailing end-of-line comment, so a hand-written
///         <c>.AddCustomers(configuration) // explanation</c> still counts as the registration snippet.
///     </para>
/// </summary>
internal static partial class SnippetEditor
{
    public const string AnchorPrefix = "// scaffold:";

    public static string AnchorComment(string anchor)
    {
        return AnchorPrefix + anchor;
    }

    public static bool HasAnchor(string text, string anchor)
    {
        return FindAnchorLines(text, anchor).Count == 1;
    }

    public static bool Contains(string text, string anchor, string snippet)
    {
        var (regionStart, anchorStart, indent) = Locate(text, anchor);
        return FindInRegion(text, regionStart, anchorStart, Indent(snippet, indent)) is not null;
    }

    /// <summary>
    ///     Whether the anchor's region mentions <paramref name="token" /> at all (e.g. the resource name), to tell an
    ///     edited snippet apart from one that is already gone.
    /// </summary>
    public static bool RegionMentions(string text, string anchor, string token)
    {
        var (regionStart, anchorStart, _) = Locate(text, anchor);
        return text[regionStart..anchorStart].Contains(token, StringComparison.Ordinal);
    }

    public static string Insert(string text, string anchor, string snippet)
    {
        var (_, anchorStart, indent) = Locate(text, anchor);
        return text.Insert(anchorStart, Indent(snippet, indent));
    }

    /// <summary>
    ///     Remove the snippet nearest above the anchor. Returns null when the snippet is not in the anchor's region.
    /// </summary>
    public static string? Remove(string text, string anchor, string snippet)
    {
        var (regionStart, anchorStart, indent) = Locate(text, anchor);
        return FindInRegion(text, regionStart, anchorStart, Indent(snippet, indent)) is { } match
            ? text.Remove(match.Start, match.Length)
            : null;
    }

    public static string Indent(string snippet, string indent)
    {
        var lines = snippet.Split('\n');
        return string.Join('\n', lines.Select(line => line.Length == 0 ? line : indent + line));
    }

    private static (int RegionStart, int AnchorStart, string Indent) Locate(string text, string anchor)
    {
        var anchors = FindAnchorLines(text, anchor);
        if (anchors.Count != 1)
        {
            throw new AnchorException(anchors.Count == 0
                ? $"anchor '{AnchorComment(anchor)}' not found"
                : $"anchor '{AnchorComment(anchor)}' appears {anchors.Count} times");
        }

        var anchorStart = anchors[0];
        var indent = new string(text[anchorStart..].TakeWhile(c => c is ' ' or '\t').ToArray());

        // The region starts after the closest anchor line (of any name) above this one.
        var regionStart = 0;
        foreach (var (lineStart, lineEnd) in Lines(text))
        {
            if (lineStart >= anchorStart)
            {
                break;
            }

            if (text[lineStart..lineEnd].Trim().StartsWith(AnchorPrefix, StringComparison.Ordinal))
            {
                regionStart = Math.Min(lineEnd + 1, text.Length);
            }
        }

        return (regionStart, anchorStart, indent);
    }

    [GeneratedRegex(@"^(?<code>\s*[^\s/].*?)\s+//.*$")]
    private static partial Regex TrailingComment();

    /// <summary>
    ///     Find the last run of whole lines in the region that matches the snippet's lines, comparing
    ///     <see cref="Normalize" />d lines. Returns the run's start and length in the original text.
    /// </summary>
    private static (int Start, int Length)? FindInRegion(string text, int regionStart, int regionEnd, string snippet)
    {
        var wanted = SplitLines(snippet).Select(Normalize).ToList();
        if (wanted.Count == 0)
        {
            return null;
        }

        var lines = new List<(int Start, int Length, string Normalized)>();
        var start = regionStart;
        foreach (var line in SplitLines(text[regionStart..regionEnd]))
        {
            lines.Add((start, line.Length + 1, Normalize(line)));
            start += line.Length + 1;
        }

        for (var first = lines.Count - wanted.Count; first >= 0; first--)
        {
            if (wanted.Select((line, offset) => lines[first + offset].Normalized == line).All(match => match))
            {
                var last = lines[first + wanted.Count - 1];
                var end = Math.Min(last.Start + last.Length, text.Length);
                return (lines[first].Start, end - lines[first].Start);
            }
        }

        return null;
    }

    /// <summary>
    ///     Lines of text that ends with a newline, without the empty entry after the final newline.
    /// </summary>
    private static List<string> SplitLines(string text)
    {
        var lines = text.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string Normalize(string line)
    {
        var match = TrailingComment().Match(line);
        return (match.Success ? match.Groups["code"].Value : line).TrimEnd();
    }

    private static List<int> FindAnchorLines(string text, string anchor)
    {
        var comment = AnchorComment(anchor);
        return Lines(text)
            .Where(line => text[line.Start..line.End].Trim() == comment)
            .Select(line => line.Start)
            .ToList();
    }

    private static IEnumerable<(int Start, int End)> Lines(string text)
    {
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            yield return (start, end);
            start = end + 1;
        }
    }
}
