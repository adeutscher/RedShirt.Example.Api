using System.Text.RegularExpressions;

namespace RedShirt.Example.Api.Scaffolding.Editing;

/// <summary>
///     Adds and removes single-line ProjectReference items as text, so the rest of the project file keeps its
///     formatting (<c>dotnet add reference</c> rewrites the whole file).
/// </summary>
internal static partial class ProjectReferenceEditor
{
    [GeneratedRegex("""^(?<indent>[ \t]*)<ProjectReference\s+Include="(?<include>[^"]+)"[^>]*/>[ \t]*$""")]
    private static partial Regex SingleLineReference();

    public static bool HasReference(string projectPath, string referencePath)
    {
        return FindReferenceLine(TextFile.Read(projectPath).Text.Split('\n'), projectPath, referencePath) >= 0;
    }

    /// <summary>
    ///     Insert a reference after the last single-line ProjectReference, matching its indentation.
    /// </summary>
    public static void Add(string projectPath, string referencePath)
    {
        var file = TextFile.Read(projectPath);
        var lines = file.Text.Split('\n').ToList();
        var last = lines.FindLastIndex(line => SingleLineReference().IsMatch(line));
        if (last < 0)
        {
            throw new InvalidOperationException(
                $"'{projectPath}' has no single-line <ProjectReference .../> to add a reference beside.");
        }

        var indent = SingleLineReference().Match(lines[last]).Groups["indent"].Value;
        lines.Insert(last + 1, $"{indent}<ProjectReference Include=\"{IncludePath(projectPath, referencePath)}\"/>");
        file.Text = string.Join('\n', lines);
        file.Write(projectPath);
    }

    /// <summary>
    ///     Remove the reference's line. Returns false when it is not a single-line ProjectReference.
    /// </summary>
    public static bool Remove(string projectPath, string referencePath)
    {
        var file = TextFile.Read(projectPath);
        var lines = file.Text.Split('\n').ToList();
        var index = FindReferenceLine(lines, projectPath, referencePath);
        if (index < 0)
        {
            return false;
        }

        lines.RemoveAt(index);
        file.Text = string.Join('\n', lines);
        file.Write(projectPath);
        return true;
    }

    private static int FindReferenceLine(IList<string> lines, string projectPath, string referencePath)
    {
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var target = Path.GetFullPath(referencePath);

        for (var i = 0; i < lines.Count; i++)
        {
            var match = SingleLineReference().Match(lines[i]);
            if (match.Success && string.Equals(
                    Path.GetFullPath(match.Groups["include"].Value.Replace('\\', Path.DirectorySeparatorChar),
                        projectDirectory),
                    target,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Relative path with backslashes, the form the repository's project files use.
    /// </summary>
    private static string IncludePath(string projectPath, string referencePath)
    {
        return Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, Path.GetFullPath(referencePath))
            .Replace('/', '\\');
    }
}
