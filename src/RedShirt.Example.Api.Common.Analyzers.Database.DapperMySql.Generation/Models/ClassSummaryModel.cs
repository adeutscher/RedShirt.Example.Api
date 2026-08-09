using RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Constants;
using System.Collections.Generic;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Models;

public class ClassSummaryModel
{
    public string Namespace { get; set; }

    /// <summary>
    ///     Root namespace for project.
    ///     In the original version of this generator, there was a BIG assumption that an API using this analyzer will always
    ///     have a 3-part root namespace (e.g.
    ///     RedShirt.Adventure.Realm, RedShirt.Adventure.World).
    ///     Since that cannot be counted on, this implementation instead assumes a common base declared in a constant.
    ///     The value of this constant is updated with the template's `init-repo.sh` initialization script.
    /// </summary>
#pragma warning disable S2325
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public string BaseNamespace => NamespaceConstants.BaseNamespace;
#pragma warning restore S2325

    public string GeneratedNamespace => Namespace + ".Generated";

    /// <summary>
    ///     Name of database table
    /// </summary>
    public string TableName { get; set; }

    public string ConnectionStringName { get; set; }

    /// <summary>
    ///     DataType Object (DTO) name
    /// </summary>
    public string DtoName { get; set; }

    /// <summary>
    ///     Fully-qualified path to DTO object
    /// </summary>
    public string FullDtoName => $"{Namespace}.{DtoName}";

    /// <summary>
    ///     Name of C# object minus 'Dto'
    /// </summary>
    public string BaseName => DtoName.Replace("Dto", "");

    /// <summary>
    ///     Skip generating POST-related classes/methods for this service.
    ///     Suggests that this DTO is for a supporting attribute whose primary key refers to the primary key of another object.
    /// </summary>
    public bool DoNotGeneratePost { get; set; }

    /// <summary>
    ///     Skip generating the service.
    ///     Suggests that some part of the DTO needs a more complex check.
    /// </summary>
    public bool DoNotGenerateService { get; set; }

    public PropertyModel Key { get; set; }
    public PropertyModel CreatedAt { get; set; }
    public PropertyModel UpdatedAt { get; set; }
    public List<PropertyModel> Properties { get; set; }
    public uint MaxSearchPageSize { get; set; }

    public bool SearchableByKey { get; set; }
    /* Shorthands to various other structure names */

    /// <summary>
    ///     Name of core-layer service
    /// </summary>
    public string ServiceClassName => $"{BaseName}Service";

    public string ServiceInterfaceName => $"I{ServiceClassName}";

    /// <summary>
    ///     Name of repository service
    /// </summary>
    public string RepositoryName => $"MariaDb{BaseName}Repository";

    /// <summary>
    ///     Name of repository interface
    /// </summary>
    public string RepositoryInterfaceName => $"I{BaseName}Repository";

    /* Request/Response Names */

    public string RequestClassPatch => $"{ServiceClassName}PatchRequest";
    public string RequestClassPost => $"{ServiceClassName}PostRequest";
    public string RequestClassPut => $"{ServiceClassName}PutRequest";
    public string RequestClassSearch => $"{ServiceClassName}SearchRequest";
    public string ResponseClassSearch => $"{BaseName}SearchResponse";
}