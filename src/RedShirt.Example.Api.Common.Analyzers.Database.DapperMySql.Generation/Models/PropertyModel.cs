using System;
using System.Collections.Generic;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Models;

public class PropertyModel
{
    public enum PropertyCategory
    {
        Bool,
        Guid,
        Integer,
        FloatingPointNumber,
        Enum,
        DateTime,
        String
    }

    public string Name { get; set; }
    public string ColumnName { get; set; }
    public string EffectiveName => string.IsNullOrEmpty(ColumnName) ? Name : ColumnName;
    public string Type { get; set; }
    public bool IsNullable { get; set; }
    public bool IsStoredAsDecimal { get; set; }
    public bool IsInternallyManaged { get; set; }
    public bool IsRangeSearchable { get; set; }
    public bool CannotFilterBy { get; set; }
    public int FilterPriorityWeight { get; set; }

    /// <summary>
    ///     CLR type used on the generated persistence entity.
    ///     <see cref="StoredAsDecimalAttribute" /> string DTO properties map to <see cref="decimal" />.
    /// </summary>
    public string EntityType =>
        IsStoredAsDecimal && Category == PropertyCategory.String ? typeof(decimal).FullName! : Type;

    /// <summary>
    ///     CLR type used on the service-layer DTO and request objects.
    ///     <see cref="StoredAsDecimalAttribute" /> string DTO properties map to <see cref="decimal" />.
    /// </summary>
    public string ServiceType => EntityType;

    public PropertyCategory Category
    {
        get
        {
            if (Type == typeof(Guid).FullName)
            {
                return PropertyCategory.Guid;
            }

            if (Type.Contains(".Enums."))
            {
                // Check for enum based on the namespace that it was placed in.
                // TODO: Improve on this check...
                return PropertyCategory.Enum;
            }

            if (Type == typeof(DateTime).FullName)
            {
                return PropertyCategory.DateTime;
            }

            if (Type == "string" || Type == typeof(string).FullName || Type == typeof(string).FullName)
            {
                return PropertyCategory.String;
            }

            if (Type == "bool")
            {
                return PropertyCategory.Bool;
            }

            if (new List<string>
                {
                    "float",
                    "double"
                }.Contains(Type))
            {
                return PropertyCategory.FloatingPointNumber;
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (new List<string>
                {
                    "int",
                    "uint",
                    "short",
                    "ushort",
                    "long",
                    "ulong",
                    "float",
                    "double",
                    "decimal"
                }.Contains(Type))
            {
                return PropertyCategory.Integer;
            }

            throw new Exception($"Unknown property type: {Type}");
        }
    }
}