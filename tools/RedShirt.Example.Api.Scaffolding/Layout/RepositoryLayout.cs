using RedShirt.Example.Api.Scaffolding.Options;

namespace RedShirt.Example.Api.Scaffolding.Layout;

/// <summary>
///     Paths in the API repository. The base namespace is read from the single .slnx at the root, the same way
///     init-repo.sh detects it, so the tool keeps working after the template is renamed.
/// </summary>
internal sealed class RepositoryLayout
{
    private RepositoryLayout(string root, string baseNamespace)
    {
        Root = root;
        BaseNamespace = baseNamespace;
    }

    public string Root { get; }

    public string BaseNamespace { get; }

    public string CoreNamespace => $"{BaseNamespace}.Core";

    public string ApiProject => Relative("src", BaseNamespace, $"{BaseNamespace}.csproj");

    public string CoreProject => Relative("src", CoreNamespace, $"{CoreNamespace}.csproj");

    public string ApiDirectory => Relative("src", BaseNamespace);

    public string CoreDirectory => Relative("src", CoreNamespace);

    public string ApiTestsDirectory => Relative("test", $"{BaseNamespace}.UnitTests");

    public string CoreTestsDirectory => Relative("test", $"{CoreNamespace}.UnitTests");

    public static RepositoryLayout Discover(string? root)
    {
        var start = Path.GetFullPath(root ?? Directory.GetCurrentDirectory());
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            var solutions = directory.GetFiles("*.slnx");
            if (solutions.Length == 1 && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return new RepositoryLayout(directory.FullName,
                    Path.GetFileNameWithoutExtension(solutions[0].Name));
            }

            if (root is not null)
            {
                break;
            }
        }

        throw new OptionsException(
            $"Could not find the repository root (one .slnx beside a src/ directory) from '{start}'.");
    }

    /// <summary>
    ///     Repository-relative path with forward slashes.
    /// </summary>
    public string Relative(params string[] parts)
    {
        return string.Join('/', parts);
    }

    public string ToRelative(string path)
    {
        return Path.GetRelativePath(Root, Path.GetFullPath(path, Root)).Replace('\\', '/');
    }

    public string Absolute(string relativePath)
    {
        return Path.GetFullPath(relativePath, Root);
    }

    /// <summary>
    ///     Resolve the data store projects for a resource. Two shapes are recognised:
    ///     a single generated project (DataStores.Order) and a Core/Implementation pair (DataStores.Product.*).
    /// </summary>
    public (string Contract, string Registration)? FindDataStoreProjects(ScaffoldOptions options)
    {
        if (options.DataStoreProject is not null)
        {
            var contract = ToRelative(options.DataStoreProject);
            return (contract, options.RegistrationProject is null ? contract : ToRelative(options.RegistrationProject));
        }

        var single = $"{BaseNamespace}.DataStores.{options.Resource}";
        var singlePath = Relative("src", single, $"{single}.csproj");
        if (File.Exists(Absolute(singlePath)))
        {
            return (singlePath, options.RegistrationProject is null ? singlePath : ToRelative(options.RegistrationProject));
        }

        var core = $"{single}.Core";
        var implementation = $"{single}.Implementation";
        var corePath = Relative("src", core, $"{core}.csproj");
        var implementationPath = Relative("src", implementation, $"{implementation}.csproj");
        if (File.Exists(Absolute(corePath)))
        {
            return (corePath, options.RegistrationProject is not null
                ? ToRelative(options.RegistrationProject)
                : File.Exists(Absolute(implementationPath))
                    ? implementationPath
                    : corePath);
        }

        return null;
    }
}
