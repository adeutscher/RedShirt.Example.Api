using Dapper;
using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Utility;

/// <summary>
///     Maps DTO string properties marked with <see cref="StoredAsDecimalAttribute" /> to/from SQL decimal values.
/// </summary>
public static class StoredAsDecimalHelper
{
    /// <summary>
    ///     Builds a SELECT list that CASTs <see cref="StoredAsDecimalAttribute" /> columns to CHAR
    ///     so Dapper can populate string DTO properties.
    /// </summary>
    public static string BuildSelectClause(Type dtoType)
    {
        ArgumentNullException.ThrowIfNull(dtoType);

        return string.Join(", ", dtoType.GetProperties().Select(property =>
        {
            var columnName = GetColumnName(property);
            var quotedColumn = DatabaseUtility.QuoteResource(columnName);

            if (IsStoredAsDecimal(property))
            {
                return $"CAST({quotedColumn} AS CHAR) AS {DatabaseUtility.QuoteResource(property.Name)}";
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
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

    public static bool IsStoredAsDecimal(PropertyInfo property)
    {
        return property.GetCustomAttribute<StoredAsDecimalAttribute>() is not null;
    }

    public static decimal ParseRequiredDecimal(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new BadRequestException($"Invalid decimal value for '{propertyName}'.");
        }

        return parsed;
    }

    /// <summary>
    ///     Creates write parameters for a DTO, parsing <see cref="StoredAsDecimalAttribute" /> strings as
    ///     <see cref="decimal" /> with invariant culture.
    /// </summary>
    public static DynamicParameters ToWriteParameters(object dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var parameters = new DynamicParameters();
        foreach (var property in dto.GetType().GetProperties())
        {
            var value = property.GetValue(dto);
            if (IsStoredAsDecimal(property))
            {
                parameters.Add(property.Name, ParseRequiredDecimal(value as string, property.Name));
                continue;
            }

            parameters.Add(property.Name, value);
        }

        return parameters;
    }
}