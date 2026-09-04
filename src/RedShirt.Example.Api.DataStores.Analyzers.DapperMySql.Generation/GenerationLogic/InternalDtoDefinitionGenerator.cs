using RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Extensions;
using RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Models;
using System.Collections.Generic;
using System.Text;

namespace RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.GenerationLogic;

public static class InternalDtoDefinitionGenerator
{
    private static IEnumerable<PropertyModel> GetAllMappedProperties(ClassSummaryModel classSummaryModel)
    {
        yield return classSummaryModel.Key;
        yield return classSummaryModel.CreatedAt;
        yield return classSummaryModel.UpdatedAt;

        foreach (var property in classSummaryModel.Properties.Where(p => !p.IsInternallyManaged))
        {
            yield return property;
        }
    }

    private static string GetToPublicDtoAssignment(PropertyModel property)
    {
        if (property is {IsStoredAsDecimal: true, Category: PropertyModel.PropertyCategory.String})
        {
            if (property.IsNullable)
            {
                return
                    $"source.{property.Name}.HasValue ? source.{property.Name}.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null";
            }

            return $"source.{property.Name}.ToString(System.Globalization.CultureInfo.InvariantCulture)";
        }

        return $"source.{property.Name}";
    }

    public static StringBuilder WriteInternalDtoInfo(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        if (!classSummaryModel.HasStoredAsDecimalProperties)
        {
            // Without the use of StoredAsDecimal, the internal DTO would be a 1-to-1 match of the external DTO.
            // Return without declaring.
            return sb;
        }

        sb.AppendLine()
            .AppendLine($"public sealed class {classSummaryModel.InternalDtoName}")
            .OpenBracket(0);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            var nullableMark = property.IsNullable ? "?" : string.Empty;
            sb.AppendLineWithIndent(
                $"public required {property.ServiceType}{nullableMark} {property.Name} " + "{ get; init; }");
        }

        sb.CloseBracket(0)
            .AppendLine();

        // Mapping: InternalDto -> public DTO (for CQRS / API boundary)
        sb.AppendLine($"public static class {classSummaryModel.InternalDtoName}Extensions")
            .OpenBracket(0)
            .AppendLineWithIndent(
                $"public static {classSummaryModel.FullDtoName} ToDto(this {classSummaryModel.FullInternalDtoName} source)")
            .OpenBracket()
            .AppendLineWithIndent(2, $"return new {classSummaryModel.FullDtoName}")
            .OpenBracket(2);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            sb.AppendLineWithIndent(3, $"{property.Name} = {GetToPublicDtoAssignment(property)},");
        }

        return sb
            .AppendLineWithIndent(2, "};")
            .CloseBracket()
            .CloseBracket(0)
            .AppendLine();
    }
}