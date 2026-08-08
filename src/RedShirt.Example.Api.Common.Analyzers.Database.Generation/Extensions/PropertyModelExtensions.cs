using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Models;
using System.Text;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.Extensions;

public static class PropertyModelExtensions
{
    /// <summary>
    /// </summary>
    /// <param name="property"></param>
    /// <param name="sb"></param>
    /// <param name="isNullable">Force nullable (e.g. for a Patch request)</param>
    /// <param name="isKey"></param>
    public static void Write(this PropertyModel property, StringBuilder sb, bool isNullable = false, bool isKey = false)
    {
        var nullableMark = string.Empty;
        if (!isKey)
        {
            nullableMark = isNullable || property.IsNullable ? "?" : string.Empty;
        }

        // if (property.Name != property.EffectiveName)
        // {
        //     sb.AppendLine($"[System.ComponentModel.DataAnnotations.Schema.Column(\"{property.ColumnName}\")]");
        // }
        sb.AppendLineWithIndent($"public required {property.Type}{nullableMark} {property.Name} " + "{ get; init; }");
    }
}