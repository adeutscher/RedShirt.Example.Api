using RedShirt.Example.Api.Scaffolding.Contracts;
using RedShirt.Example.Api.Scaffolding.Editing;
using RedShirt.Example.Api.Scaffolding.Execution;
using RedShirt.Example.Api.Scaffolding.Layout;
using RedShirt.Example.Api.Scaffolding.Options;
using RedShirt.Example.Api.Scaffolding.Planning;
using RedShirt.Example.Api.Scaffolding.Rendering;

namespace RedShirt.Example.Api.Scaffolding;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Any(arg => arg is "-h" or "--help"))
        {
            Console.WriteLine(ScaffoldOptionsParser.Usage);
            return args.Length == 0 ? ExitCodes.Usage : ExitCodes.Success;
        }

        ScaffoldOptions options;
        RepositoryLayout layout;
        try
        {
            options = ScaffoldOptionsParser.Parse(args);
            layout = RepositoryLayout.Discover(options.RepositoryRoot);
        }
        catch (OptionsException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            await Console.Error.WriteLineAsync("Run with --help for usage.");
            return ExitCodes.Usage;
        }

        try
        {
            return await RunAsync(options, layout);
        }
        catch (Exception exception) when (exception is OptionsException or AnchorException)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return ExitCodes.Failure;
        }
    }

    private static async Task<int> RunAsync(ScaffoldOptions options, RepositoryLayout layout)
    {
        var dataStore = layout.FindDataStoreProjects(options);
        ResourceContract? contract = null;
        if (dataStore is { } projects)
        {
            Console.WriteLine($"Reading the {options.Resource} service contract from {projects.Contract}");
            contract = await ContractLoader.LoadAsync(options.Resource,
                layout.Absolute(projects.Contract), layout.Absolute(projects.Registration), options.Registration,
                CancellationToken.None);
        }
        else if (options.Mode == ScaffoldMode.Create)
        {
            throw new OptionsException(
                $"No data store project found for {options.Resource} under src/. Pass --data-store <csproj>.");
        }
        else
        {
            Console.WriteLine($"No data store project found for {options.Resource}. " +
                              "Files can only be removed with --force, and some snippets need removing by hand.");
        }

        var model = ResourceModel.Create(layout, options, contract);
        var plan = ScaffoldPlanner.Build(layout, model, dataStore, TemplateRenderer.Default);
        var reporter = new Reporter(Console.Out, options.DryRun);
        var executor = new ScaffoldExecutor(layout, options, reporter, Console.In, Console.Out,
            !Console.IsInputRedirected);

        var result = options.Mode == ScaffoldMode.Create ? executor.Create(plan) : executor.Remove(plan);

        if (options.ManifestPath is not null && !options.DryRun && result == ExitCodes.Success)
        {
            reporter.WriteManifest(options.ManifestPath);
            Console.WriteLine($"Manifest written to {options.ManifestPath} ({reporter.ChangedPaths.Count} paths)");
        }

        if (result == ExitCodes.Success && !options.DryRun && reporter.ChangedPaths.Count > 0)
        {
            Console.WriteLine("Next: build and test to check the result, e.g. 'dotnet test'.");
        }

        return result;
    }
}
