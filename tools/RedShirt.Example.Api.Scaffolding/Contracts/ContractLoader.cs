using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using RedShirt.Example.Api.Scaffolding.Options;

namespace RedShirt.Example.Api.Scaffolding.Contracts;

/// <summary>
///     Reads a data store's service contract with Roslyn. The project is opened with MSBuildWorkspace, so the
///     compilation includes source-generated types (IOrderService, OrderServiceSearchRequest, etc).
/// </summary>
internal static class ContractLoader
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static async Task<ResourceContract> LoadAsync(string resource, string contractProject,
        string registrationProject, string? registrationName, CancellationToken cancellationToken)
    {
        using var workspace = MSBuildWorkspace.Create();
        var contractCompilation = await CompileAsync(workspace, contractProject, cancellationToken);
        var registrationCompilation = registrationProject == contractProject
            ? contractCompilation
            : await CompileAsync(workspace, registrationProject, cancellationToken);

        var serviceName = $"I{resource}Service";
        var service = FindType(contractCompilation.Assembly.GlobalNamespace, serviceName)
                      ?? throw new OptionsException(
                          $"'{contractProject}' does not declare {serviceName}. If the data store is source-generated, " +
                          "build it first so the generator output is available.");

        var getById = Method(service, "GetByIdAsync");
        var delete = Method(service, "DeleteAsync");
        var post = Method(service, "PostAsync");
        var put = Method(service, "PutAsync");
        var patch = Method(service, "PatchAsync");
        var search = Method(service, "SearchAsync");

        var dtoSymbol = (getById ?? post ?? put ?? patch) is { } dtoSource
            ? UnwrapTask(dtoSource.ReturnType)
            : throw new OptionsException($"{serviceName} has no GetByIdAsync, PostAsync, PutAsync, or PatchAsync.");

        var keySymbol = (getById ?? delete)?.Parameters.FirstOrDefault()?.Type
                        ?? throw new OptionsException(
                            $"{serviceName} has no GetByIdAsync or DeleteAsync to give the key type.");

        var putRequest = RequestType(put);
        var patchRequest = RequestType(patch);
        var keyName = (patchRequest ?? putRequest)?.Properties.FirstOrDefault()?.Name ?? "Id";

        var registration = FindRegistration(registrationCompilation, registrationProject, registrationName);

        return new ResourceContract
        {
            Service = ToType(service, false),
            Dto = ToType(dtoSymbol, true),
            Key = ToProperty(keyName, keySymbol),
            HasGet = getById is not null,
            HasDelete = delete is not null,
            PostRequest = RequestType(post),
            PutRequest = putRequest,
            PatchRequest = patchRequest,
            SearchRequest = RequestType(search),
            SearchResponse = search is null ? null : ToType(UnwrapTask(search.ReturnType), true),
            ContinuationTokenType = search?.Parameters.Skip(1).FirstOrDefault()?.Type.ToDisplayString(TypeFormat),
            RegistrationMethod = registration.Name,
            RegistrationNamespace = registration.ContainingNamespace.ToDisplayString(),
            RegistrationTakesConfiguration = registration.Parameters.Length == 2
        };
    }

    private static async Task<Compilation> CompileAsync(MSBuildWorkspace workspace, string projectPath,
        CancellationToken cancellationToken)
    {
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        return await project.GetCompilationAsync(cancellationToken)
               ?? throw new OptionsException($"Could not compile '{projectPath}'.");
    }

    private static INamedTypeSymbol? FindType(INamespaceSymbol ns, string name)
    {
        foreach (var type in ns.GetTypeMembers(name))
        {
            return type;
        }

        return ns.GetNamespaceMembers().Select(child => FindType(child, name)).FirstOrDefault(found => found is not null);
    }

    private static IMethodSymbol? Method(INamedTypeSymbol service, string name)
    {
        return service.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault();
    }

    private static ITypeSymbol UnwrapTask(ITypeSymbol type)
    {
        return type is INamedTypeSymbol {Name: "Task" or "ValueTask", TypeArguments.Length: 1} task
            ? task.TypeArguments[0]
            : type;
    }

    private static ContractType? RequestType(IMethodSymbol? method)
    {
        return method?.Parameters.FirstOrDefault()?.Type is INamedTypeSymbol request ? ToType(request, true) : null;
    }

    private static ContractType ToType(ITypeSymbol type, bool withProperties)
    {
        var properties = withProperties
            ? type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property is
                    {IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public})
                .Select(property => ToProperty(property.Name, property.Type))
                .ToList()
            : [];

        return new ContractType(type.ToDisplayString(TypeFormat), NamespaceOf(type), properties);
    }

    private static ContractProperty ToProperty(string name, ITypeSymbol type)
    {
        var isNullableValue = type is INamedTypeSymbol {OriginalDefinition.SpecialType: SpecialType.System_Nullable_T};
        var underlying = isNullableValue ? ((INamedTypeSymbol) type).TypeArguments[0] : type;
        var isNullable = isNullableValue || type.NullableAnnotation == NullableAnnotation.Annotated;
        var nonNullable = underlying.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(TypeFormat);

        return new ContractProperty(
            name,
            type.ToDisplayString(TypeFormat),
            nonNullable,
            NamespaceOf(underlying),
            isNullable,
            underlying.IsReferenceType,
            underlying.SpecialType == SpecialType.System_String,
            underlying.TypeKind == TypeKind.Array,
            SampleValue(name, underlying, nonNullable));
    }

    private static string SampleValue(string name, ITypeSymbol type, string display)
    {
        if (type.TypeKind == TypeKind.Array)
        {
            return "[]";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return type is INamedTypeSymbol named && named.MemberNames.FirstOrDefault() is { } first
                ? $"{display}.{first}"
                : $"default({display})";
        }

        return type.SpecialType switch
        {
            SpecialType.System_String => $"\"{name} value\"",
            SpecialType.System_Boolean => "true",
            SpecialType.System_Byte => "(byte) 7",
            SpecialType.System_Int16 => "(short) 7",
            SpecialType.System_Int32 => "7",
            SpecialType.System_Int64 => "7L",
            SpecialType.System_Single => "1.5f",
            SpecialType.System_Double => "1.5d",
            SpecialType.System_Decimal => "12.34m",
            SpecialType.System_DateTime => "DateTime.UtcNow",
            _ => type.ToDisplayString() switch
            {
                "System.Guid" => "Guid.NewGuid()",
                "System.DateTimeOffset" => "DateTimeOffset.UtcNow",
                "System.DateOnly" => "DateOnly.FromDateTime(DateTime.UtcNow)",
                "System.TimeOnly" => "TimeOnly.FromDateTime(DateTime.UtcNow)",
                "System.TimeSpan" => "TimeSpan.FromMinutes(5)",
                _ => type.IsReferenceType ? $"new {display}()" : $"default({display})"
            }
        };
    }

    /// <summary>
    ///     Namespace to import for a type: "" for built-in, System, and global types.
    /// </summary>
    private static string NamespaceOf(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None || type.ContainingNamespace is not {IsGlobalNamespace: false} ns)
        {
            return "";
        }

        var name = ns.ToDisplayString();
        return name == "System" || name.StartsWith("System.", StringComparison.Ordinal) ? "" : name;
    }

    private static IMethodSymbol FindRegistration(Compilation compilation, string project,
        string? name)
    {
        var candidates = AllTypes(compilation.Assembly.GlobalNamespace)
            .Where(type => type is {IsStatic: true, DeclaredAccessibility: Accessibility.Public})
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .Where(method => method is {IsExtensionMethod: true, DeclaredAccessibility: Accessibility.Public}
                             && method.Parameters[0].Type.Name == "IServiceCollection"
                             && method.ReturnType.Name == "IServiceCollection"
                             && (name is null || method.Name == name))
            .ToList();

        if (candidates.Count != 1)
        {
            var found = candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(method => method.Name));
            throw new OptionsException(
                $"Expected one public IServiceCollection extension method in '{project}', found {found}. " +
                "Pass --registration <name> (or --registration-project <csproj>).");
        }

        var registration = candidates[0];
        if (registration.Parameters.Length > 2 ||
            (registration.Parameters.Length == 2 && !registration.Parameters[1].Type.Name.StartsWith("IConfiguration",
                StringComparison.Ordinal)))
        {
            throw new OptionsException(
                $"{registration.Name} must take no arguments or a single IConfiguration argument.");
        }

        return registration;
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var type in ns.GetNamespaceMembers().SelectMany(AllTypes))
        {
            yield return type;
        }
    }
}
