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

    public static StringBuilder WriteDtoEntityMapping(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        var serviceDto = classSummaryModel.FullServiceDtoName;

        sb.AppendLineWithIndent(
                $"private static {serviceDto} ToDto({classSummaryModel.FullEntityName} entity)")
            .OpenBracket()
            .AppendLineWithIndent(2, $"return new {serviceDto}")
            .OpenBracket(2);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            sb.AppendLineWithIndent(3, $"{property.Name} = entity.{property.Name},");
        }

        sb
            .AppendLineWithIndent(2, "};")
            .CloseBracket()
            .AppendLine();

        sb.AppendLineWithIndent(
                $"private static {classSummaryModel.FullEntityName} ToEntity({serviceDto} dto)")
            .OpenBracket()
            .AppendLineWithIndent(2, $"return new {classSummaryModel.FullEntityName}")
            .OpenBracket(2);

        foreach (var property in GetAllMappedProperties(classSummaryModel))
        {
            sb.AppendLineWithIndent(3, $"{property.Name} = dto.{property.Name},");
        }

        return sb
            .AppendLineWithIndent(2, "};")
            .CloseBracket()
            .AppendLine();
    }
}