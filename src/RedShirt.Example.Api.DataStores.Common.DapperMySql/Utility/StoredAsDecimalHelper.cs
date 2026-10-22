using Dapper;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.Utility;

/// <summary>
///     Dapper parameter and SELECT helpers for MySQL DTO types.
/// </summary>
public static class StoredAsDecimalHelper
{
    public static string BuildSelectClause(Type dtoType)
    {
        ArgumentNullException.ThrowIfNull(dtoType);

        return string.Join(", ", dtoType.GetProperties().Select(property =>
        {
            var columnName = GetColumnName(property);
            var quotedColumn = DatabaseUtility.QuoteResource(columnName);

            if (!string.Equals(columnName, property.Name, StringComparison.Ordinal))
            {
                return $"{quotedColumn} AS {DatabaseUtility.QuoteResource(property.Name)}";
            }

            return quotedColumn;
        }));
    }

    public static string GetColumnName(PropertyInfo property)
    {
        var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
        if (columnAttribute is not null && !string.IsNullOrWhiteSpace(columnAttribute.Name))
        {
            return columnAttribute.Name;
        }

        return property.Name;
    }

    public static DynamicParameters ToWriteParameters(object dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var parameters = new DynamicParameters();
        foreach (var property in dto.GetType().GetProperties())
        {
            parameters.Add(property.Name, property.GetValue(dto));
        }

        return parameters;
    }
}
