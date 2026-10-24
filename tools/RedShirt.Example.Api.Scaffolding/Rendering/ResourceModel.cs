using RedShirt.Example.Api.Scaffolding.Contracts;
using RedShirt.Example.Api.Scaffolding.Layout;
using RedShirt.Example.Api.Scaffolding.Options;
using RedShirt.Example.Api.Scaffolding.Utility;

namespace RedShirt.Example.Api.Scaffolding.Rendering;

/// <summary>
///     Everything the templates can see, exposed to Scriban as <c>model</c> with snake_case member names
///     (e.g. <c>model.dto_type</c>). Members that come from the service contract are null when the contract
///     could not be loaded (remove mode after the data store is gone).
/// </summary>
internal sealed class ResourceModel
{
    private ResourceModel(RepositoryLayout layout, ScaffoldOptions options, ResourceContract? contract)
    {
        Name = options.Resource;
        BaseNamespace = layout.BaseNamespace;
        Route = options.Route ?? Naming.DefaultRoute(options.Resource);
        CustomerScope = options.CustomerScope;
        Contract = contract;
    }

    public ResourceContract? Contract { get; }

    public string Name { get; }
    public string CamelName => Naming.ToCamelCase(Name);
    public string PermissionPrefix => Naming.ToKebabCase(Name);
    public string Route { get; }
    public bool CustomerScope { get; }

    public string BaseNamespace { get; }
    public string ApiNamespace => BaseNamespace;
    public string CoreNamespace => $"{BaseNamespace}.Core";
    public string UseCaseNamespace => $"{CoreNamespace}.UseCases.{Name}";

    // Contract-derived members. Templates that render files only run when Contract is set.
    public string ServiceInterface => Required.Service.Name;
    public string ServiceNamespace => Required.Service.Namespace;
    public string ServiceVariable => $"{CamelName}Service";
    public string DtoType => Required.Dto.Name;
    public string DtoNamespace => Required.Dto.Namespace;
    public IReadOnlyList<ContractProperty> DtoProperties => Required.Dto.Properties;
    public ContractProperty Key => Required.Key;
    public string KeyVariable => Naming.ToCamelCase(Key.Name);
    public bool KeyIsGuid => Key.NonNullableType == "Guid";

    public string KeyRouteConstraint => Key.NonNullableType switch
    {
        "Guid" => ":guid",
        "int" => ":int",
        "long" => ":long",
        _ => ""
    };

    public string KeyRouteTemplate => $"{{{KeyVariable}{KeyRouteConstraint}}}";

    public bool HasCreate => Required.PostRequest is not null;
    public bool HasGet => Required.HasGet;
    public bool HasUpdate => Required.PutRequest is not null;
    public bool HasPatch => Required.PatchRequest is not null;
    public bool HasDelete => Required.HasDelete;
    public bool HasSearch => Required.SearchRequest is not null;

    public ContractType? PostRequest => Required.PostRequest;
    public ContractType? PutRequest => Required.PutRequest;
    public ContractType? PatchRequest => Required.PatchRequest;
    public ContractType? SearchRequest => Required.SearchRequest;
    public ContractType? SearchResponse => Required.SearchResponse;

    public IReadOnlyList<ContractProperty> PostProperties => Required.PostRequest?.Properties ?? [];

    /// <summary>
    ///     Put/Patch request properties without the key, which comes from the route.
    /// </summary>
    public IReadOnlyList<ContractProperty> PutProperties => WithoutKey(Required.PutRequest);

    public IReadOnlyList<ContractProperty> PatchProperties => WithoutKey(Required.PatchRequest);
    public IReadOnlyList<ContractProperty> SearchProperties => Required.SearchRequest?.Properties ?? [];
    public string ContinuationTokenType => Required.ContinuationTokenType ?? "Guid?";

    public IReadOnlyList<ContractProperty> PostNullable => PostProperties.Where(property => property.IsNullable).ToList();
    public IReadOnlyList<ContractProperty> PostNonNullable => PostProperties.Where(property => !property.IsNullable).ToList();
    public IReadOnlyList<ContractProperty> PutNullable => PutProperties.Where(property => property.IsNullable).ToList();
    public IReadOnlyList<ContractProperty> PutNonNullable => PutProperties.Where(property => !property.IsNullable).ToList();

    public IReadOnlyList<ContractProperty> PostRequiredStrings => RequiredStrings(PostProperties);
    public IReadOnlyList<ContractProperty> PutRequiredStrings => RequiredStrings(PutProperties);

    /// <summary>
    ///     Namespaces needed by property types (e.g. enums declared beside the DTO).
    /// </summary>
    public IReadOnlyList<string> PostNamespaces => Namespaces(PostProperties);
    public IReadOnlyList<string> PutNamespaces => Namespaces([Key, .. PutProperties]);
    public IReadOnlyList<string> PatchNamespaces => Namespaces([Key, .. PatchProperties]);
    public IReadOnlyList<string> SearchNamespaces => Namespaces(SearchProperties);
    public IReadOnlyList<string> KeyNamespaces => Namespaces([Key]);
    public IReadOnlyList<string> DtoPropertyNamespaces => Namespaces(DtoProperties);

    public string RegistrationCall => Required.RegistrationTakesConfiguration
        ? $"{Required.RegistrationMethod}(configuration)"
        : $"{Required.RegistrationMethod}()";

    public string RegistrationNamespace => Required.RegistrationNamespace;

    private ResourceContract Required => Contract
                                         ?? throw new InvalidOperationException(
                                             "This template needs the data store's service contract.");

    public static ResourceModel Create(RepositoryLayout layout, ScaffoldOptions options, ResourceContract? contract)
    {
        var model = new ResourceModel(layout, options, contract);
        if (contract is not null && options.CustomerScope)
        {
            var dtoCustomer = contract.Dto.Properties.FirstOrDefault(property => property.Name == "CustomerId");
            var searchCustomer =
                contract.SearchRequest?.Properties.FirstOrDefault(property => property.Name == "CustomerId");
            if (!contract.HasGet)
            {
                throw new OptionsException("--scope customer needs GetByIdAsync to load the record before checking scope.");
            }

            if (dtoCustomer is not {NonNullableType: "Guid", IsNullable: false} ||
                (contract.SearchRequest is not null && searchCustomer is null))
            {
                throw new OptionsException(
                    $"--scope customer needs a non-nullable Guid CustomerId on {contract.Dto.Name} " +
                    "(and a CustomerId search filter).");
            }
        }

        return model;
    }

    private IReadOnlyList<ContractProperty> WithoutKey(ContractType? request)
    {
        return request?.Properties.Where(property => property.Name != Key.Name).ToList() ?? [];
    }

    private static IReadOnlyList<ContractProperty> RequiredStrings(IEnumerable<ContractProperty> properties)
    {
        return properties.Where(property => property is {IsString: true, IsNullable: false}).ToList();
    }

    private static IReadOnlyList<string> Namespaces(IEnumerable<ContractProperty> properties)
    {
        return properties.Select(property => property.Namespace).Where(ns => ns.Length > 0).Distinct().ToList();
    }
}
