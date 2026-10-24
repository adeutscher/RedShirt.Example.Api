namespace RedShirt.Example.Api.Scaffolding.Execution;

/// <summary>
///     Announces each file action as it happens and remembers the paths that changed, for the manifest.
/// </summary>
internal sealed class Reporter(TextWriter output, bool dryRun)
{
    private readonly List<string> _changed = [];

    public IReadOnlyList<string> ChangedPaths => _changed;

    public void Created(string path, string? detail = null)
    {
        Changed("created", path, detail);
    }

    public void Modified(string path, string? detail = null)
    {
        Changed("modified", path, detail);
    }

    public void Removed(string path, string? detail = null)
    {
        Changed("removed", path, detail);
    }

    public void Skipped(string path, string detail)
    {
        Write("skipped", path, detail);
    }

    public void Kept(string path, string detail)
    {
        Write("kept", path, detail);
    }

    public void Warning(string path, string detail)
    {
        Write("warning", path, detail);
    }

    public void WriteManifest(string manifestPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(manifestPath, _changed);
    }

    private void Changed(string verb, string path, string? detail)
    {
        var dryRunVerb = verb switch
        {
            "created" => "would create",
            "modified" => "would modify",
            _ => "would remove"
        };
        Write(dryRun ? dryRunVerb : verb, path, detail);
        if (!_changed.Contains(path, StringComparer.Ordinal))
        {
            _changed.Add(path);
        }
    }

    private void Write(string verb, string path, string? detail)
    {
        output.WriteLine(detail is null ? $"{verb,-14} {path}" : $"{verb,-14} {path} ({detail})");
    }
}
