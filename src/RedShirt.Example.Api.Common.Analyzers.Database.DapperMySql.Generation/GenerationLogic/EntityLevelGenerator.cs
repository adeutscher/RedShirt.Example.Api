using RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Extensions;
using RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Models;
using System.Linq;
using System.Text;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.GenerationLogic;

public static class EntityLevelGenerator
{
    private const string AttributesNamespace =
        "RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes";

    public static StringBuilder WriteEntityInfo(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine()
            .AppendLine(
                $"[{AttributesNamespace}.DbTable(\"{classSummaryModel.TableName}\", \"{classSummaryModel.ConnectionStringName}\")]")
            .AppendLine($"internal sealed class {classSummaryModel.EntityName}")
            .OpenBracket(0);

        WriteEntityProperty(sb, classSummaryModel.Key, isKey: true);
        WriteEntityProperty(sb, classSummaryModel.CreatedAt, isCreatedAt: true);
        WriteEntityProperty(sb, classSummaryModel.UpdatedAt, isUpdatedAt: true);

        foreach (var property in classSummaryModel.Properties.Where(p => !p.IsInternallyManaged))
        {
            WriteEntityProperty(sb, property);
        }

        return sb
            .CloseBracket(0)
            .AppendLine();
    }

    private static void WriteEntityProperty(
        StringBuilder sb,
        PropertyModel property,
        bool isKey = false,
        bool isCreatedAt = false,
        bool isUpdatedAt = false)
    {
        if (isKey)
        {
            sb.AppendLineWithIndent($"[{AttributesNamespace}.DbKey]");
        }

        if (isCreatedAt)
        {
            sb.AppendLineWithIndent($"[{AttributesNamespace}.CreatedAtProperty]");
        }

        if (isUpdatedAt)
        {
            sb.AppendLineWithIndent($"[{AttributesNamespace}.UpdatedAtProperty]");
        }

        var nullableMark = !isKey && property.IsNullable ? "?" : string.Empty;
        sb.AppendLineWithIndent(
            $"public required {property.EntityType}{nullableMark} {property.Name} " + "{ get; init; }");
    }
}
