using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Extensions;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Models;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.GenerationLogic;

public static class ServiceLevelGenerator
{
    private static StringBuilder WriteSupportingExtensions(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        var partsForIsTheSameAs = classSummaryModel
            .Properties
            .Where(p => !p.IsInternallyManaged)
            .Select(p => $"a.{p.Name} == b.{p.Name}");

        sb.AppendLine("internal static class SupportingExtensions {")
            .AppendLineWithIndent(
                $"public static bool IsTheSameAs(this {classSummaryModel.FullDtoName} a, {classSummaryModel.FullDtoName} b)")
            .OpenBracket()
            .AppendLineWithIndent($"return {string.Join(" && ", partsForIsTheSameAs)};")
            .CloseBracket();

        var partsForAreChangesRequested = classSummaryModel
            .Properties
            .Where(p => !p.IsInternallyManaged)
            .SelectMany(p =>
            {
                var returnList = new List<string>();

                if (p.IsInternallyManaged)
                {
                    return returnList;
                }

                if (p.Category == PropertyModel.PropertyCategory.String)
                {
                    returnList.Add($"!string.IsNullOrWhiteSpace(subject.{p.Name})");
                }
                else
                {
                    returnList.Add($"subject.{p.Name}.HasValue");
                }

                if (p.IsNullable)
                {
                    returnList.Add($"subject.Clear{p.Name}.HasValue");
                }

                return returnList;
            });

        sb.AppendLineWithIndent(
                $"public static bool AreChangesRequested(this {classSummaryModel.RequestClassPatch} subject)")
            .OpenBracket()
            .AppendLineWithIndent($"return {string.Join(" || ", partsForAreChangesRequested)};")
            .CloseBracket();

        return sb.AppendLine("}");
    }

    private static StringBuilder InsertValidation(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        foreach (var property in classSummaryModel.Properties)
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (property.Category)
            {
                case PropertyModel.PropertyCategory.String:

                    if (property.IsNullable)
                    {
                        break;
                    }

                    sb
                        .AppendLineWithIndent(2, $"if(string.IsNullOrWhiteSpace(request.{property.Name}))")
                        .OpenBracket(2)
                        .AppendLineWithIndent(3,
                            $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.BadRequestException(\"{property.Name} cannot be empty.\");")
                        .CloseBracket(2);

                    break;
            }
        }

        return sb;
    }

    private static StringBuilder WriteServiceClassContent(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        if (classSummaryModel.DoNotGenerateService)
        {
            // abort
            return sb;
        }

        sb.AppendLine(
            $"internal partial class {classSummaryModel.ServiceClassName}({classSummaryModel.RepositoryInterfaceName} repository) : {classSummaryModel.ServiceInterfaceName}" +
            " {");

        /* Delete */
        sb
            .AppendLineWithIndent(
                $"public async {typeof(Task).FullName} DeleteAsync({typeof(Guid).FullName} id, {typeof(CancellationToken).FullName} cancellationToken = default)" +
                "{")
            .AppendLineWithIndent("if (!await repository.DeleteAsync(id, cancellationToken))", 2)
            .OpenBracket(2)
            .AppendLineWithIndent(
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.ResourceNotFoundException();", 3)
            .CloseBracket(2)
            .CloseBracket();

        /* Get */
        sb.AppendLineWithIndent(
                $"public async {Helper.Taskify($"{classSummaryModel.FullDtoName}")} GetByIdAsync(Guid id, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent("if (await repository.GetByIdAsync(id, cancellationToken) is not { } entry)", 2)
            .OpenBracket(2)
            .AppendLineWithIndent(
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.ResourceNotFoundException();", 3)
            .CloseBracket(2)
            .AppendLineWithIndent(2, "return entry;")
            .CloseBracket();

        /* Patch */
        sb.AppendLineWithIndent(
                $"public async {Helper.Taskify(classSummaryModel.FullDtoName)} PatchAsync({classSummaryModel.RequestClassPatch} request, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent(2, "if (!request.AreChangesRequested())")
            .OpenBracket(2)
            .AppendLineWithIndent(
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.NoChangesToModifyException();", 2)
            .CloseBracket(2)
            .AppendLineWithIndent(
                $"if (await repository.GetByIdAsync(request.{classSummaryModel.Key.Name}, cancellationToken) is not " +
                " { } existing)", 2)
            .OpenBracket(2)
            .AppendLineWithIndent(
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.ResourceNotFoundException();", 3)
            .CloseBracket(2)
            .AppendLineWithIndent(2, $"var candidate = new {classSummaryModel.FullDtoName}")
            .OpenBracket(2)
            .AppendLineWithIndent(3, $"{classSummaryModel.Key.Name} = request.{classSummaryModel.Key.Name},")
            .AppendLineWithIndent(3,
                $"{classSummaryModel.CreatedAt.Name} = existing.{classSummaryModel.CreatedAt.Name},")
            .AppendLineWithIndent(3, $"{classSummaryModel.UpdatedAt.Name} = {typeof(DateTime).FullName}.UtcNow,");

        foreach (var property in classSummaryModel.Properties)
        {
            if (property.IsInternallyManaged)
            {
                continue;
            }

            if (property.IsNullable)
            {
                sb.AppendLineWithIndent(3,
                    $"{property.Name} = request.Clear{property.Name} == true ? null : request.{property.Name} ?? existing.{property.Name},");
            }
            else
            {
                sb.AppendLineWithIndent(3, $"{property.Name} = request.{property.Name} ?? existing.{property.Name},");
            }
        }

        sb.AppendLineWithIndent(2, "};");

        sb.AppendLineWithIndent(2, "if (candidate.IsTheSameAs(existing))")
            .OpenBracket(2)
            .AppendLineWithIndent(
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.NoChangesToModifyException();", 2)
            .CloseBracket(2);

        sb.AppendLineWithIndent(2, "return await repository.UpsertAsync(candidate, cancellationToken);");

        sb.CloseBracket(); // End Patch

        /* Post */
        if (!classSummaryModel.DoNotGeneratePost)
        {
            sb.AppendLineWithIndent(
                    $"public async {Helper.Taskify(classSummaryModel.FullDtoName)} PostAsync({classSummaryModel.RequestClassPost} request, {typeof(CancellationToken).FullName} cancellationToken = default)")
                .OpenBracket()
                .InsertValidation(classSummaryModel)
                .AppendLineWithIndent(2, "var createdAt = DateTime.UtcNow;")
                .AppendLineWithIndent(2, $"var dto = new {classSummaryModel.FullDtoName}")
                .OpenBracket(2)
                .AppendLineWithIndent(3, $"{classSummaryModel.Key.Name} = {typeof(Guid).FullName}.NewGuid(),")
                .AppendLineWithIndent(3, $"{classSummaryModel.CreatedAt.Name} = createdAt,")
                .AppendLineWithIndent(3, $"{classSummaryModel.UpdatedAt.Name} = createdAt,");

            foreach (var property in classSummaryModel.Properties)
            {
                if (property.IsInternallyManaged)
                {
                    continue;
                }

                sb.AppendLineWithIndent(3, $"{property.Name} = request.{property.Name},");
            }

            sb.AppendLineWithIndent(2, "};")
                .AppendLineWithIndent("return await repository.UpsertAsync(dto, cancellationToken);")
                .CloseBracket();

            // End Post
        }

        /* Put */
        sb.AppendLineWithIndent(
                $"public async {Helper.Taskify(classSummaryModel.FullDtoName)} PutAsync({classSummaryModel.RequestClassPut} request, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .InsertValidation(classSummaryModel)
            .AppendLineWithIndent(2,
                $"var existing = await repository.GetByIdAsync(request.{classSummaryModel.Key.Name}, cancellationToken);")
            .AppendLineWithIndent(2, "var createdAt = DateTime.UtcNow;")
            .AppendLineWithIndent(2, $"var dto = new {classSummaryModel.FullDtoName}")
            .OpenBracket(2)
            .AppendLineWithIndent(3, $"{classSummaryModel.Key.Name} = request.{classSummaryModel.Key.Name},")
            .AppendLineWithIndent(3,
                $"{classSummaryModel.CreatedAt.Name} = existing?.{classSummaryModel.CreatedAt.Name} ?? createdAt,")
            .AppendLineWithIndent(3, $"{classSummaryModel.UpdatedAt.Name} = createdAt,");

        foreach (var property in classSummaryModel.Properties)
        {
            if (property.IsInternallyManaged)
            {
                continue;
            }

            sb.AppendLineWithIndent(3, $"{property.Name} = request.{property.Name},");
        }

        sb
            .AppendLineWithIndent(2, "};")
            .AppendLineWithIndent(2, "if (existing is not null && existing.IsTheSameAs(dto))")
            .OpenBracket(2)
            .AppendLineWithIndent(3,
                $"throw new {classSummaryModel.BaseNamespace}.Common.Exceptions.NoChangesToModifyException();")
            .CloseBracket(2)
            .AppendLineWithIndent(2, "return await repository.UpsertAsync(dto, cancellationToken);")
            .CloseBracket();

        /* Search */
        sb.AppendLineWithIndent(
                $"public {Helper.Taskify(classSummaryModel.ResponseClassSearch)} SearchAsync({classSummaryModel.RequestClassSearch} parameters,{typeof(Guid).FullName}? continuationToken,{typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent(2, "return repository.SearchAsync(parameters, continuationToken, cancellationToken);")
            .CloseBracket();

        sb.AppendLine("}"); // close

        return sb;
    }

    private static StringBuilder WriteServiceInterface(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine($"public partial interface {classSummaryModel.ServiceInterfaceName} " + "{");
        sb.AppendLineWithIndent(
            $"{typeof(Task).FullName} DeleteAsync({typeof(Guid).FullName} id, {typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify($"{classSummaryModel.FullDtoName}")} GetByIdAsync(Guid id, {typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify($"{classSummaryModel.FullDtoName}")} PatchAsync({classSummaryModel.RequestClassPatch} request, {typeof(CancellationToken).FullName} cancellationToken = default);");
        if (!classSummaryModel.DoNotGeneratePost)
        {
            sb.AppendLineWithIndent(
                $"{Helper.Taskify($"{classSummaryModel.FullDtoName}")} PostAsync({classSummaryModel.RequestClassPost} request, {typeof(CancellationToken).FullName} cancellationToken = default);");
        }

        sb.AppendLineWithIndent(
            $"{Helper.Taskify($"{classSummaryModel.FullDtoName}")} PutAsync({classSummaryModel.RequestClassPut} request, {typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify(classSummaryModel.ResponseClassSearch)} SearchAsync({classSummaryModel.RequestClassSearch} parameters,{typeof(Guid).FullName}? continuationToken,{typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WriteRepositoryInterface(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine($"internal interface {classSummaryModel.RepositoryInterfaceName} " + "{");

        sb.AppendLineWithIndent(
            $"{Helper.Taskify("bool")} DeleteAsync({typeof(Guid).FullName} id, {typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify($"{classSummaryModel.FullDtoName}?")} GetByIdAsync(Guid id, {typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify(classSummaryModel.ResponseClassSearch)}  SearchAsync({classSummaryModel.RequestClassSearch} parameters, {typeof(Guid).FullName}? continuationToken,{typeof(CancellationToken).FullName} cancellationToken = default);");
        sb.AppendLineWithIndent(
            $"{Helper.Taskify($"{classSummaryModel.FullDtoName}")} UpsertAsync({classSummaryModel.FullDtoName} item, {typeof(CancellationToken).FullName} cancellationToken = default);");

        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WritePatchRequest(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine(
            $"public class {classSummaryModel.RequestClassPatch} " + "{");

        classSummaryModel.Key.Write(sb);
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var property in classSummaryModel.Properties)
        {
            if (property.IsInternallyManaged)
            {
                continue;
            }

            property.Write(sb, true);

            // ReSharper disable once InvertIf
            if (property.IsNullable)
            {
                var clearProperty = new PropertyModel
                {
                    Name = $"Clear{property.Name}",
                    IsNullable = true,
                    Type = "bool",
                    IsInternallyManaged = false
                };
                clearProperty.Write(sb);
            }
        }

        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WritePostRequest(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        if (classSummaryModel.DoNotGeneratePost)
        {
            // Abort
            return sb;
        }

        sb.AppendLine($"public class {classSummaryModel.RequestClassPost} " +
                      "{");

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var property in classSummaryModel.Properties)
        {
            if (property.IsInternallyManaged)
            {
                continue;
            }

            property.Write(sb);
        }

        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WritePutRequest(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine($"public class {classSummaryModel.RequestClassPut} " + "{");

        classSummaryModel.Key.Write(sb, isKey: true);
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var property in classSummaryModel.Properties)
        {
            if (property.IsInternallyManaged)
            {
                continue;
            }

            property.Write(sb);
        }

        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WriteSearchRequest(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine($"public class {classSummaryModel.RequestClassSearch} " +
                      "{")
            .AppendLineWithIndent("public required int PageSize { get; init; }");

        var propertiesToConsider = classSummaryModel.Properties.ToList();
        if (classSummaryModel.SearchableByKey)
        {
            propertiesToConsider.Insert(0, classSummaryModel.Key);
        }

        foreach (var property in propertiesToConsider)
        {
            if (property.CannotFilterBy)
            {
                // Skip
                continue;
            }

            // Add direct match for every type except DateTime and FloatingPointNumber
            if (property.Category != PropertyModel.PropertyCategory.DateTime
                && property.Category != PropertyModel.PropertyCategory.FloatingPointNumber)
            {
                if (property.IsRangeSearchable)
                {
                    var arrayProperty = new PropertyModel
                    {
                        Name = $"{property.Name}Range",
                        Type = $"{property.Type}[]"
                    };
                    arrayProperty.Write(sb);
                }
                else
                {
                    property.Write(sb, true);
                }
            }

            // Add special filters by type (e.g. text contains, greater-than/less-than)
            switch (property.Category)
            {
                case PropertyModel.PropertyCategory.String:
                {
                    var contains = new PropertyModel
                    {
                        Name = $"{property.Name}Contains",
                        Type = "string",
                        IsNullable = true
                    };
                    contains.Write(sb);
                    break;
                }
                case PropertyModel.PropertyCategory.DateTime:
                {
                    /*
                     * Attempt to get a simplified name.
                     * At the moment it's made for properties like 'CreatedAtUtc' and 'UpdatedAtUtc'
                     */
                    var malleableName = Regex.Replace(property.Name, "Utc$", string.Empty);
                    var hadUtc = malleableName != property.Name;
                    malleableName = Regex.Replace(malleableName, "At$", string.Empty);
                    var restoreUtc = hadUtc ? "Utc" : string.Empty;

                    var before = new PropertyModel
                    {
                        Name = $"{malleableName}Before{restoreUtc}",
                        Type = typeof(DateTime).FullName!,
                        IsNullable = true
                    };
                    before.Write(sb);
                    var after = new PropertyModel
                    {
                        Name = $"{malleableName}After{restoreUtc}",
                        Type = typeof(DateTime).FullName!,
                        IsNullable = true
                    };
                    after.Write(sb);
                    break;
                }
                case PropertyModel.PropertyCategory.FloatingPointNumber:
                case PropertyModel.PropertyCategory.Integer:
                {
                    var greaterThan = new PropertyModel
                    {
                        Name = $"{property.Name}GreaterThan",
                        Type = property.Type,
                        IsNullable = true
                    };
                    greaterThan.Write(sb);
                    var lessThan = new PropertyModel
                    {
                        Name = $"{property.Name}LessThan",
                        Type = property.Type,
                        IsNullable = true
                    };
                    lessThan.Write(sb);
                    break;
                }
            } // end switch

            if (property.IsNullable)
            {
                var isNull = new PropertyModel
                {
                    Name = $"{property.Name}IsNull",
                    Type = "bool",
                    IsNullable = false
                };
                isNull.Write(sb);
            }
        } // end foreach

        sb.AppendLine("}");

        return sb;
    }

    private static StringBuilder WriteSearchResponse(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        sb.AppendLine($"public class {classSummaryModel.ResponseClassSearch} " + "{");

        var ct = new PropertyModel
        {
            Name = "ContinuationToken",
            Type = typeof(Guid).FullName!,
            IsNullable = true
        };
        ct.Write(sb);

        var items = new PropertyModel
        {
            Name = "Records",
            Type = Helper.Listify(classSummaryModel.FullDtoName),
            IsNullable = false
        };
        items.Write(sb);

        sb.AppendLine("}");

        return sb;
    }

    public static StringBuilder WriteServiceInfo(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        return sb
            // Requests/Responses
            .WritePatchRequest(classSummaryModel)
            .WritePutRequest(classSummaryModel)
            .WritePostRequest(classSummaryModel)
            .WriteSearchRequest(classSummaryModel)
            .WriteSearchResponse(classSummaryModel)
            // Repository interface
            .WriteRepositoryInterface(classSummaryModel)
            // Supporting Extensions
            .WriteSupportingExtensions(classSummaryModel)
            // Core service
            .WriteServiceInterface(classSummaryModel)
            .WriteServiceClassContent(classSummaryModel);
    }
}