namespace RedShirt.Example.Api.Scaffolding.Planning;

/// <summary>
///     A whole file owned by the resource.
/// </summary>
/// <param name="Path">Repository-relative path.</param>
/// <param name="Content">Rendered content, or null when the service contract was not available to render it.</param>
internal sealed record FileTarget(string Path, string? Content);

/// <summary>
///     A snippet inserted into a shared file just above an anchor comment (<c>// scaffold:{Anchor}</c>).
/// </summary>
/// <param name="Path">Repository-relative path of the shared file.</param>
/// <param name="Anchor">Anchor name, without the <c>scaffold:</c> prefix.</param>
/// <param name="Content">Rendered snippet at column 0, or null when it could not be rendered.</param>
internal sealed record SnippetTarget(string Path, string Anchor, string? Content);

/// <summary>
///     A ProjectReference from one of the API projects to the resource's data store.
/// </summary>
/// <param name="Project">Repository-relative path of the project gaining the reference.</param>
/// <param name="Reference">Repository-relative path of the referenced project.</param>
internal sealed record ProjectReferenceTarget(string Project, string Reference);

internal sealed record ScaffoldPlan(
    IReadOnlyList<FileTarget> Files,
    IReadOnlyList<SnippetTarget> Snippets,
    IReadOnlyList<ProjectReferenceTarget> References,
    IReadOnlyList<string> PruneRoots);
