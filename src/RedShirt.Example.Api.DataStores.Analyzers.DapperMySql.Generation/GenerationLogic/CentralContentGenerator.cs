using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;
using RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Exceptions;
using RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Models;
using RedShirt.Example.Api.DataStores.Constants;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.GenerationLogic;

public static class CentralContentGenerator
{
    private static TableProperties GetTableProperties(INamedTypeSymbol classSymbol)
    {
        var classAttributes = classSymbol.GetAttributes();
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (classAttributes.FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(DbTableAttribute)) is not
            { } dbTableAttribute)
        {
            throw new DbTableAttributeNotFoundException();
        }

        var tableName = dbTableAttribute.ConstructorArguments[0].Value!.ToString();
        var connectionStringName = dbTableAttribute.ConstructorArguments[1].Value?.ToString() ??
                                   DatabaseConstants.PrimaryDatabaseConnectionStringName;
        var keySearchable = dbTableAttribute.ConstructorArguments[2].Value!.ToString().ToLower() == "true";

        var maxPageSize = (uint) 100; // Default
        if (classAttributes.FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(DbMaxPageSizeAttribute)) is
                { } pageSizeAttribute
            // ReSharper disable once MergeIntoPattern
            && pageSizeAttribute.ConstructorArguments.Length > 0
            && uint.TryParse(pageSizeAttribute.ConstructorArguments[0].Value?.ToString(), out var newPageSize))
        {
            maxPageSize = newPageSize;
        }

        return new TableProperties
        {
            TableName = tableName,
            ConnectionStringName = connectionStringName,
            KeySearchable = keySearchable,
            MaxPageSize = maxPageSize
        };
    }

    private static bool DetermineDoNotGeneratePost(INamedTypeSymbol classSymbol)
    {
        return
            classSymbol.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(DoNotGeneratePostAttribute)) is
                not null;
    }

    private static bool DetermineDoNotGenerateService(INamedTypeSymbol classSymbol)
    {
        return
            classSymbol.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(DoNotGenerateServiceAttribute)) is
                not null;
    }

    private static ClassSummaryModel GetClassSummary(INamedTypeSymbol classSymbol)
    {
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;

        var tableProperties = GetTableProperties(classSymbol);

        var props = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        IPropertySymbol? keyProp = null;
        IPropertySymbol? createdProp = null;
        IPropertySymbol? updatedProp = null;
        var properties = new List<PropertyModel>();
        foreach (var prop in props)
        {
            if (prop.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(DbKeyAttribute)) is not null)
            {
                keyProp = prop;
                continue;
            }

            if (prop.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(CreatedAtPropertyAttribute)) is not
                null)
            {
                createdProp = prop;
            }

            if (prop.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(UpdatedAtPropertyAttribute)) is not
                null)
            {
                updatedProp = prop;
            }

            var attributeModel = prop.ToAttributeModel();
            properties.Add(attributeModel);
        }

        var doNotGeneratePost = DetermineDoNotGeneratePost(classSymbol);

        if (!doNotGeneratePost && keyProp!.ToAttributeModel().Category != PropertyModel.PropertyCategory.Guid)
        {
            throw new UnsupportedKeyTypeForPost(
                $"Currently, only {typeof(Guid).FullName} keys are supported for POST calls. If this was in error, then mark the class with [DoNotGeneratePost] attribute.");
        }

        return new ClassSummaryModel
        {
            Namespace = ns,
            TableName = tableProperties.TableName,
            ConnectionStringName = tableProperties.ConnectionStringName,
            MaxSearchPageSize = tableProperties.MaxPageSize,
            SearchableByKey = tableProperties.KeySearchable,
            DtoName = className,
            DoNotGeneratePost = doNotGeneratePost,
            DoNotGenerateService = DetermineDoNotGenerateService(classSymbol),
            Key = keyProp!.ToAttributeModel(),
            CreatedAt = createdProp!.ToAttributeModel(),
            UpdatedAt = updatedProp!.ToAttributeModel(),
            Properties = properties
        };
    }

    private static PropertyModel ToAttributeModel(this IPropertySymbol prop)
    {
        var classAttributes = prop.GetAttributes();
        /* Get Filter Weight */
        var filterWeight = 1; // Default
        if (classAttributes.FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(FilterPriorityWeightAttribute))
                is
                { } filterWeightAttribute
            // ReSharper disable once MergeIntoPattern
            && filterWeightAttribute.ConstructorArguments.Length > 0
            && int.TryParse(filterWeightAttribute.ConstructorArguments[0].Value!.ToString(),
                out var tentativeFilterWeight))
        {
            filterWeight = tentativeFilterWeight;
        }

        /* Return Model */
        return new PropertyModel
        {
            Name = prop.Name,
            ColumnName = string.Empty,
            IsNullable = prop.Type.ToDisplayString().Contains("?"),
            Type = prop.Type.ToDisplayString().Replace("?", string.Empty),
            IsStoredAsDecimal = prop.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(StoredAsDecimalAttribute)) is not null,
            FilterPriorityWeight = filterWeight,
            CannotFilterBy = prop.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(CannotFilterByAttribute)) is not null,
            IsRangeSearchable = prop.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(RangeSearchableAttribute)) is not null,
            IsInternallyManaged =
                prop.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(CreatedAtPropertyAttribute)) is not null
                || prop.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass!.Name == nameof(UpdatedAtPropertyAttribute)) is not null
        };
    }

    public static void Generate(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        var classSummary = GetClassSummary(classSymbol);

        // Header
        var sb = new StringBuilder();
        sb.AppendLine("// AUTO-GENERATED-CODE, PLEASE DO NOT MODIFY DIRECTLY");
        sb.AppendLine($"// Built: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {classSummary.GeneratedNamespace};");
        sb.WriteEntityInfo(classSummary);
        sb.WriteInternalDtoInfo(classSummary);
        sb
            .WriteServiceInfo(classSummary)
            .WriteRepositoryInfo(classSummary)
            .GetDependencyInjectionStatement(classSummary);
        sb.AppendLine("#nullable disable");

        var filePath = $"{classSummary.BaseName}.g.cs";
        context.AddSource(filePath, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private class TableProperties
    {
        public string TableName { get; set; }
        public string ConnectionStringName { get; set; }
        public bool KeySearchable { get; set; }
        public uint MaxPageSize { get; set; }
    }
}