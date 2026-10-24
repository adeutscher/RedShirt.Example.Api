namespace RedShirt.Example.Api.Scaffolding.Editing;

internal sealed class AnchorException(string message) : Exception(message);

/// <summary>
///     Inserts and removes snippets above <c>// scaffold:{anchor}</c> comments in shared files.
///     <para>
///         A snippet belongs to the region between its anchor and the anchor line above it (or the start of the file).
///         That keeps identical snippets for different anchors in one file (e.g. two permission lists in the same
///         test class) apart. Snippets are rendered at column 0 and indented to match the anchor.
///     </para>
/// </summary>
internal static class SnippetEditor
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
        return FindInRegion(text, regionStart, anchorStart, Indent(snippet, indent)) >= 0;
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
        var indented = Indent(snippet, indent);
        var index = FindInRegion(text, regionStart, anchorStart, indented);
        return index < 0 ? null : text.Remove(index, indented.Length);
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

    private static int FindInRegion(string text, int regionStart, int regionEnd, string value)
    {
        var region = text[regionStart..regionEnd];
        for (var index = region.LastIndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = index == 0 ? -1 : region.LastIndexOf(value, index - 1, StringComparison.Ordinal))
        {
            if (index == 0 || region[index - 1] == '\n')
            {
                return regionStart + index;
            }
        }

        return -1;
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
