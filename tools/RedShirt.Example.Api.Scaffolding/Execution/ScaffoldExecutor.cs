using RedShirt.Example.Api.Scaffolding.Editing;
using RedShirt.Example.Api.Scaffolding.Layout;
using RedShirt.Example.Api.Scaffolding.Options;
using RedShirt.Example.Api.Scaffolding.Planning;

namespace RedShirt.Example.Api.Scaffolding.Execution;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int Usage = 2;
    public const int Cancelled = 3;
}

internal sealed class ScaffoldExecutor(
    RepositoryLayout layout,
    ScaffoldOptions options,
    Reporter reporter,
    TextReader input,
    TextWriter output,
    bool inputIsInteractive)
{
    public int Create(ScaffoldPlan plan)
    {
        var problems = plan.Snippets
            .Select(snippet => SnippetProblem(snippet))
            .Concat(plan.References
                .Where(reference => !File.Exists(layout.Absolute(reference.Project)))
                .Select(reference => $"{reference.Project}: project not found"))
            .OfType<string>()
            .ToList();
        if (problems.Count > 0)
        {
            output.WriteLine("Nothing was changed. Fix these first:");
            foreach (var problem in problems)
            {
                output.WriteLine($"  * {problem}");
            }

            return ExitCodes.Failure;
        }

        foreach (var file in plan.Files)
        {
            var path = layout.Absolute(file.Path);
            if (File.Exists(path))
            {
                reporter.Skipped(file.Path, "exists");
                continue;
            }

            if (!options.DryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Content);
            }

            reporter.Created(file.Path);
        }

        foreach (var snippet in plan.Snippets)
        {
            var path = layout.Absolute(snippet.Path);
            var text = TextFile.Read(path);
            if (SnippetEditor.Contains(text.Text, snippet.Anchor, snippet.Content!))
            {
                reporter.Skipped(snippet.Path, $"{snippet.Anchor} already present");
                continue;
            }

            if (!options.DryRun)
            {
                text.Text = SnippetEditor.Insert(text.Text, snippet.Anchor, snippet.Content!);
                text.Write(path);
            }

            reporter.Modified(snippet.Path, snippet.Anchor);
        }

        foreach (var reference in plan.References)
        {
            var project = layout.Absolute(reference.Project);
            var target = layout.Absolute(reference.Reference);
            if (ProjectReferenceEditor.HasReference(project, target))
            {
                reporter.Skipped(reference.Project, $"already references {Path.GetFileName(reference.Reference)}");
                continue;
            }

            if (!options.DryRun)
            {
                ProjectReferenceEditor.Add(project, target);
            }

            reporter.Modified(reference.Project, $"reference {Path.GetFileName(reference.Reference)}");
        }

        return ExitCodes.Success;
    }

    public int Remove(ScaffoldPlan plan)
    {
        var removals = new List<(string Path, string? Detail, Action Apply)>();
        var notes = new List<(string Verb, string Path, string Detail)>();

        foreach (var file in plan.Files)
        {
            var path = layout.Absolute(file.Path);
            if (!File.Exists(path))
            {
                continue;
            }

            if (file.Content is null && !options.Force)
            {
                notes.Add(("kept", file.Path, "cannot verify without the data store contract, use --force"));
            }
            else if (file.Content is not null && !TextFile.Matches(path, file.Content) && !options.Force)
            {
                notes.Add(("kept", file.Path, "modified since scaffolding, use --force"));
            }
            else
            {
                removals.Add((file.Path, null, () => File.Delete(path)));
            }
        }

        foreach (var snippet in plan.Snippets)
        {
            var path = layout.Absolute(snippet.Path);
            if (snippet.Content is null)
            {
                notes.Add(("warning", snippet.Path, $"{snippet.Anchor}: cannot render without the data store contract, check by hand"));
                continue;
            }

            if (SnippetProblem(snippet) is { } problem)
            {
                notes.Add(("warning", snippet.Path, problem));
                continue;
            }

            var text = TextFile.Read(path);
            if (!SnippetEditor.Contains(text.Text, snippet.Anchor, snippet.Content))
            {
                if (SnippetEditor.RegionMentions(text.Text, snippet.Anchor, options.Resource))
                {
                    notes.Add(("warning", snippet.Path,
                        $"{snippet.Anchor}: snippet edited since scaffolding, remove by hand"));
                }

                continue;
            }

            removals.Add((snippet.Path, snippet.Anchor, () =>
            {
                var current = TextFile.Read(path);
                current.Text = SnippetEditor.Remove(current.Text, snippet.Anchor, snippet.Content)!;
                current.Write(path);
            }));
        }

        foreach (var reference in plan.References)
        {
            var project = layout.Absolute(reference.Project);
            var target = layout.Absolute(reference.Reference);
            if (File.Exists(project) && ProjectReferenceEditor.HasReference(project, target))
            {
                removals.Add((reference.Project, $"reference {Path.GetFileName(reference.Reference)}",
                    () => ProjectReferenceEditor.Remove(project, target)));
            }
        }

        if (plan.References.Count == 0)
        {
            notes.Add(("warning", layout.CoreProject,
                "data store project not found, check project references by hand"));
        }

        if (removals.Count == 0)
        {
            WriteNotes(notes);
            output.WriteLine($"Nothing to remove for {options.Resource}.");
            return ExitCodes.Success;
        }

        if (!options.DryRun && !options.AssumeYes)
        {
            output.WriteLine($"This will remove the following for {options.Resource}:");
            foreach (var removal in removals)
            {
                output.WriteLine(removal.Detail is null ? $"  * {removal.Path}" : $"  * {removal.Path} ({removal.Detail})");
            }

            WriteNotes(notes);

            if (!inputIsInteractive)
            {
                output.WriteLine("No terminal to confirm on. Re-run with -y to remove without a prompt.");
                return ExitCodes.Usage;
            }

            output.Write($"Type {options.Resource} to confirm: ");
            output.Flush();
            if (input.ReadLine()?.Trim() != options.Resource)
            {
                output.WriteLine("Cancelled. Nothing was changed.");
                return ExitCodes.Cancelled;
            }
        }
        else
        {
            WriteNotes(notes);
        }

        foreach (var removal in removals)
        {
            if (!options.DryRun)
            {
                removal.Apply();
            }

            reporter.Removed(removal.Path, removal.Detail);
        }

        if (!options.DryRun)
        {
            foreach (var root in plan.PruneRoots)
            {
                PruneEmptyDirectories(layout.Absolute(root));
            }
        }

        return ExitCodes.Success;
    }

    private string? SnippetProblem(SnippetTarget snippet)
    {
        var path = layout.Absolute(snippet.Path);
        if (!File.Exists(path))
        {
            return $"{snippet.Path}: file not found";
        }

        return SnippetEditor.HasAnchor(TextFile.Read(path).Text, snippet.Anchor)
            ? null
            : $"{snippet.Path}: needs exactly one '{SnippetEditor.AnchorComment(snippet.Anchor)}' line";
    }

    private void WriteNotes(IEnumerable<(string Verb, string Path, string Detail)> notes)
    {
        foreach (var (verb, path, detail) in notes)
        {
            if (verb == "kept")
            {
                reporter.Kept(path, detail);
            }
            else
            {
                reporter.Warning(path, detail);
            }
        }
    }

    private static void PruneEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(directory => directory.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
        }
    }
}
