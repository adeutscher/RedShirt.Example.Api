using RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Models;
using System.Text;

namespace RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Extensions;

public static class PropertyModelExtensions
{
    /// <summary>
    /// </summary>
    /// <param name="property"></param>
    /// <param name="sb"></param>
    /// <param name="isNullable">Force nullable (e.g. for a Patch request)</param>
    /// <param name="isKey"></param>
    /// <param name="useServiceType">
    ///     When true, emit <see cref="PropertyModel.ServiceType" /> (decimals for StoredAsDecimal properties).
    /// </param>
    public static void Write(this PropertyModel property, StringBuilder sb, bool isNullable = false,
        bool isKey = false, bool useServiceType = false)
    {
        var nullableMark = string.Empty;
        if (!isKey)
        {
            nullableMark = isNullable || property.IsNullable ? "?" : string.Empty;
        }

        var type = useServiceType ? property.ServiceType : property.Type;
        sb.AppendLineWithIndent($"public required {type}{nullableMark} {property.Name} " + "{ get; init; }");
    }
}