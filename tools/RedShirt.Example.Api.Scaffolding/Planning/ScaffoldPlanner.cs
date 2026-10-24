using RedShirt.Example.Api.Scaffolding.Layout;
using RedShirt.Example.Api.Scaffolding.Rendering;

namespace RedShirt.Example.Api.Scaffolding.Planning;

/// <summary>
///     Maps templates to output paths. Paths depend only on the resource name, so remove can find every candidate
///     file even when the service contract is no longer available to render (and compare) its content.
/// </summary>
internal static class ScaffoldPlanner
{
    private sealed record UseCase(
        string Key,
        string Kind,
        string Folder,
        Func<string, string> TypeName,
        Func<ResourceModel, bool> Applies,
        Func<ResourceModel, bool> HasValidatorTests);

    private static readonly UseCase[] UseCases =
    [
        new("Create", "Commands", "Create", name => $"Create{name}Command",
            model => model.HasCreate, model => model.PostRequiredStrings.Count > 0),
        new("Delete", "Commands", "Delete", name => $"Delete{name}Command",
            model => model.HasDelete, model => model.KeyIsGuid),
        new("Patch", "Commands", "Patch", name => $"Patch{name}Command",
            model => model.HasPatch, _ => false),
        new("Update", "Commands", "Update", name => $"Update{name}Command",
            model => model.HasUpdate, model => model.PutRequiredStrings.Count > 0),
        new("GetRecord", "Queries", "GetRecord", name => $"Get{name}RecordQuery",
            model => model.HasGet, model => model.KeyIsGuid),
        new("SearchRecords", "Queries", "SearchRecords", name => $"Search{name}RecordsQuery",
            model => model.HasSearch, _ => false)
    ];

    private sealed record ApiModel(string Suffix, Func<ResourceModel, bool> Applies);

    private static readonly ApiModel[] ApiModels =
    [
        new("PostRequest", model => model.HasCreate),
        new("PutRequest", model => model.HasUpdate),
        new("PatchRequest", model => model.HasPatch),
        new("SearchRequest", model => model.HasSearch)
    ];

    public static ScaffoldPlan Build(RepositoryLayout layout, ResourceModel model,
        (string Contract, string Registration)? dataStore, TemplateRenderer renderer)
    {
        var name = model.Name;
        var hasContract = model.Contract is not null;
        var files = new List<FileTarget>();

        void AddFile(string template, string path, Func<ResourceModel, bool>? applies = null)
        {
            if (!hasContract)
            {
                files.Add(new FileTarget(path, null));
            }
            else if (applies is null || applies(model))
            {
                files.Add(new FileTarget(path, renderer.Render(template, model)));
            }
        }

        foreach (var useCase in UseCases)
        {
            var type = useCase.TypeName(name);
            var source = layout.Relative(layout.CoreDirectory, "UseCases", name, useCase.Kind, useCase.Folder);
            var tests = layout.Relative(layout.CoreTestsDirectory, "Tests", "UseCases", name, useCase.Kind,
                useCase.Folder);

            AddFile($"core/{useCase.Key}/Request.sbn", $"{source}/{type}.cs", useCase.Applies);
            AddFile($"core/{useCase.Key}/Handler.sbn", $"{source}/{type}Handler.cs", useCase.Applies);
            AddFile($"core/{useCase.Key}/Validator.sbn", $"{source}/{type}Validator.cs", useCase.Applies);
            AddFile($"tests/core/{useCase.Key}/HandlerTests.sbn", $"{tests}/{type}HandlerTests.cs", useCase.Applies);
            AddFile($"tests/core/{useCase.Key}/ValidatorTests.sbn", $"{tests}/{type}ValidatorTests.cs",
                m => useCase.Applies(m) && useCase.HasValidatorTests(m));
        }

        AddFile("tests/core/TestData.sbn",
            layout.Relative(layout.CoreTestsDirectory, "Tests", "UseCases", name, $"{name}TestData.cs"));

        AddFile("api/Controller.sbn", layout.Relative(layout.ApiDirectory, "Controllers", $"{name}Controller.cs"));
        foreach (var apiModel in ApiModels)
        {
            AddFile($"api/{apiModel.Suffix}.sbn",
                layout.Relative(layout.ApiDirectory, "Models", name, $"{name}{apiModel.Suffix}.cs"), apiModel.Applies);
        }

        AddFile("api/ApproveReadOnlyAttribute.sbn",
            layout.Relative(layout.ApiDirectory, "Attributes", "Authorization", $"Approve{name}ReadOnlyAttribute.cs"));
        AddFile("api/AuthorizeWriteAttribute.sbn",
            layout.Relative(layout.ApiDirectory, "Attributes", "Authorization", $"Authorize{name}WriteAttribute.cs"));
        AddFile("tests/api/ControllerTests.sbn",
            layout.Relative(layout.ApiTestsDirectory, "Tests", "Controllers", $"{name}ControllerTests.cs"));

        var authorization = layout.Relative(layout.ApiDirectory, "Authorization");
        var extensions = layout.Relative(layout.ApiDirectory, "Extensions");
        var roleMapTests = layout.Relative(layout.ApiTestsDirectory, "Tests", "Authorization",
            "BespokeRolePermissionMapTests.cs");
        var claimsTransformationTests = layout.Relative(layout.ApiTestsDirectory, "Tests", "Authorization",
            "BespokeRolePermissionClaimsTransformationTests.cs");

        SnippetTarget Snippet(string path, string anchor, string template, bool needsContract = false)
        {
            return new SnippetTarget(path, anchor,
                needsContract && !hasContract ? null : renderer.Render($"snippets/{template}.sbn", model));
        }

        var snippets = new List<SnippetTarget>
        {
            Snippet($"{authorization}/Constants/BespokeAuthorizationPermissions.cs", "permissions", "permissions"),
            Snippet($"{authorization}/Constants/BespokeAuthorizationPolicies.cs", "policies", "policies"),
            Snippet($"{extensions}/AuthorizationServiceCollectionExtensions.cs", "policy-registrations",
                "policy-registrations"),
            Snippet($"{authorization}/BespokeRolePermissionMap.cs", "full-access-permissions",
                "full-access-permissions"),
            Snippet(roleMapTests, "test-full-access-permissions", "full-access-permissions"),
            Snippet(roleMapTests, "test-admin-permissions", "full-access-permissions"),
            Snippet(claimsTransformationTests, "test-claims-full-access-permissions", "full-access-permissions"),
            Snippet($"{extensions}/ServiceCollectionExtensions.cs", "data-store-usings", "data-store-using", true),
            Snippet($"{extensions}/ServiceCollectionExtensions.cs", "data-store-registrations",
                "data-store-registration", true)
        };

        var references = dataStore is { } projects
            ? new List<ProjectReferenceTarget>
            {
                new(layout.CoreProject, projects.Contract),
                new(layout.ApiProject, projects.Registration)
            }
            : [];

        var pruneRoots = new[]
        {
            layout.Relative(layout.CoreDirectory, "UseCases", name),
            layout.Relative(layout.CoreTestsDirectory, "Tests", "UseCases", name),
            layout.Relative(layout.ApiDirectory, "Models", name)
        };

        return new ScaffoldPlan(files, snippets, references, pruneRoots);
    }
}
