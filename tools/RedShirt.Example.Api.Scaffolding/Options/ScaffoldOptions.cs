using System.Text.RegularExpressions;

namespace RedShirt.Example.Api.Scaffolding.Options;

internal enum ScaffoldMode
{
    Create,
    Remove
}

internal sealed record ScaffoldOptions
{
    public required ScaffoldMode Mode { get; init; }

    /// <summary>
    ///     PascalCase resource name, matching the data store's DTO name minus "Dto" (e.g. "Order").
    /// </summary>
    public required string Resource { get; init; }

    public string? RepositoryRoot { get; init; }

    /// <summary>
    ///     Data store project that declares the service contract. Discovered by convention when omitted.
    /// </summary>
    public string? DataStoreProject { get; init; }

    /// <summary>
    ///     Data store project that declares the IServiceCollection registration. Defaults to
    ///     <see cref="DataStoreProject" />.
    /// </summary>
    public string? RegistrationProject { get; init; }

    /// <summary>
    ///     Name of the IServiceCollection extension method that registers the data store.
    /// </summary>
    public string? Registration { get; init; }

    public string? Route { get; init; }

    /// <summary>
    ///     Emit Order-style customer resource scoping in the controller.
    /// </summary>
    public bool CustomerScope { get; init; }

    public bool DryRun { get; init; }

    /// <summary>
    ///     Remove content that no longer matches the templates.
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    ///     Skip the remove confirmation prompt.
    /// </summary>
    public bool AssumeYes { get; init; }

    public string? ManifestPath { get; init; }
}

internal sealed class OptionsException(string message) : Exception(message);

internal static partial class ScaffoldOptionsParser
{
    public const string Usage = """
                                Usage: scaffold <create|remove> <Resource> [options]

                                Scaffolds (or removes) the API controller, CQRS use cases, request models,
                                authorization wiring, and test skeletons for a data store resource.

                                Arguments:
                                  Resource                 PascalCase name, the DTO name minus "Dto" (e.g. Order).

                                Options:
                                  --data-store <csproj>    Project declaring I<Resource>Service. Default: discovered under src/.
                                  --registration-project <csproj>
                                                           Project declaring the IServiceCollection registration.
                                                           Default: the .Implementation sibling, or --data-store.
                                  --registration <name>    Registration extension method name. Default: discovered.
                                  --route <route>          Controller route. Default: kebab-case plural (beans-widgets).
                                  --scope customer         Enforce customer resource scope (needs a Guid CustomerId).
                                  --manifest <file>        Write the paths created, modified, or removed to <file>.
                                  --dry-run                Report what would change without writing anything.
                                  --force                  remove: also delete files and snippets edited since scaffolding.
                                  -y, --yes                remove: skip the confirmation prompt.
                                  --repo-root <dir>        Repository root. Default: found from the working directory.
                                  -h, --help               Show this help.

                                Pass the same --route and --scope to remove that were given to create.
                                """;

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex ResourcePattern();

    public static ScaffoldOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            throw new OptionsException("Expected a mode (create or remove) and a resource name.");
        }

        var mode = args[0] switch
        {
            "create" => ScaffoldMode.Create,
            "remove" => ScaffoldMode.Remove,
            _ => throw new OptionsException($"Unknown mode '{args[0]}'. Expected create or remove.")
        };

        var resource = args[1];
        if (!ResourcePattern().IsMatch(resource))
        {
            throw new OptionsException($"Resource '{resource}' must be a PascalCase identifier (e.g. Order).");
        }

        var options = new ScaffoldOptions {Mode = mode, Resource = resource};

        for (var i = 2; i < args.Count; i++)
        {
            var arg = args[i];
            options = arg switch
            {
                "--data-store" => options with {DataStoreProject = Value(args, ref i)},
                "--registration-project" => options with {RegistrationProject = Value(args, ref i)},
                "--registration" => options with {Registration = Value(args, ref i)},
                "--route" => options with {Route = Value(args, ref i)},
                "--scope" => options with {CustomerScope = ParseScope(Value(args, ref i))},
                "--manifest" => options with {ManifestPath = Value(args, ref i)},
                "--repo-root" => options with {RepositoryRoot = Value(args, ref i)},
                "--dry-run" => options with {DryRun = true},
                "--force" => options with {Force = true},
                "-y" or "--yes" => options with {AssumeYes = true},
                _ => throw new OptionsException($"Unknown option '{arg}'.")
            };
        }

        if (mode == ScaffoldMode.Create && (options.Force || options.AssumeYes))
        {
            throw new OptionsException("--force and -y only apply to remove.");
        }

        return options;
    }

    private static bool ParseScope(string value)
    {
        return value == "customer"
            ? true
            : throw new OptionsException($"Unknown scope '{value}'. The only supported scope is 'customer'.");
    }

    private static string Value(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new OptionsException($"Option '{args[i]}' needs a value.");
        }

        i++;
        return args[i];
    }
}
