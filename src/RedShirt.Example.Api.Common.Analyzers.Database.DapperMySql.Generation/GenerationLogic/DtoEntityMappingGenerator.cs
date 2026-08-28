using RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Extensions;
using RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Models;
using System.Collections.Generic;
using System.Text;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.GenerationLogic;

public static class DtoEntityMappingGenerator
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

    private static string GetToDtoAssignment(PropertyModel property)
    {
        if (property is {IsStoredAsDecimal: true, Category: PropertyModel.PropertyCategory.String})
        {
            if (property.IsNullable)
            {
                return
                    $"entity.{property.Name}.HasValue ? entity.{property.Name}.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null";
            }

            return $"entity.{property.Name}.ToString(System.Globalization.CultureInfo.InvariantCulture)";
        }

        return $"entity.{property.Name}";
    }

    private static string GetToEntityAssignment(
        PropertyModel property,
        ClassSummaryModel classSummaryModel,
        string parseDecimal)
    {
        if (property is {IsStoredAsDecimal: true, Category: PropertyModel.PropertyCategory.String})
        {
            if (property.IsNullable)
            {
                return
                    $"dto.{property.Name} is null ? null : {parseDecimal}(dto.{property.Name}, nameof({classSummaryModel.FullDtoName}.{property.Name}))";
            }

            return $"{parseDecimal}(dto.{property.Name}, nameof({classSummaryModel.FullDtoName}.{property.Name}))";
        }

        return $"dto.{property.Name}";
    }

    public static StringBuilder WriteDtoEntityMapping(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        var baseNamespace = classSummaryModel.BaseNamespace;
        var parseDecimal =
            $"{baseNamespace}.Common.Database.DapperMySql.Utility.StoredAsDecimalHelper.ParseRequiredDecimal";

        sb.AppendLineWithIndent(
                $"private static {classSummaryModel.FullDtoName} ToDto({classSummaryModel.FullEntityName} entity)")
            .OpenBracket()
            .AppendLineWithIndent(2, $"return new {classSummaryModel.FullDtoName}")
            .OpenBracket(2);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            sb.AppendLineWithIndent(3, $"{property.Name} = {GetToDtoAssignment(property)},");
        }

        sb
            // Close bracket without a newline just for semicolon
            .AppendLineWithIndent(2, "};")
            .CloseBracket()
            .AppendLine();

        sb.AppendLineWithIndent(
                $"private static {classSummaryModel.FullEntityName} ToEntity({classSummaryModel.FullDtoName} dto)")
            .OpenBracket()
            .AppendLineWithIndent(2, $"return new {classSummaryModel.FullEntityName}")
            .OpenBracket(2);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            sb.AppendLineWithIndent(3,
                $"{property.Name} = {GetToEntityAssignment(property, classSummaryModel, parseDecimal)},");
        }

        return sb
            .CloseBracket(2)
            .AppendLineWithIndent(2, ";")
            .CloseBracket()
            .AppendLine();
    }
}