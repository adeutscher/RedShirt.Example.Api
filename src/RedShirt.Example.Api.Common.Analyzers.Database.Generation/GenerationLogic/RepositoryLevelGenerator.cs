using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Exceptions;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Extensions;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Models;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.Utility;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.GenerationLogic;

public static class RepositoryLevelGenerator
{
    private static StringBuilder AddSearchMethod(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        return sb.AppendLineWithIndent(
                $"public async {Helper.Taskify(classSummaryModel.ResponseClassSearch)} SearchAsync"
                + $"({classSummaryModel.RequestClassSearch} parameters,"
                + $" {typeof(Guid).FullName}? continuationToken, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            // Load continuation parameters
            .AppendLineWithIndent(2,
                "var continuationParameters = continuationToken.HasValue ? await cacheService.GetAsync<ContinuationParameters>(continuationToken.Value, cancellationToken) : null;")
            .AppendLineWithIndent(2, "if (continuationParameters is not null)")
            .OpenBracket(2)
            .AppendLineWithIndent(3, "// Override parameters with stored parameters for continuation token")
            .AppendLineWithIndent(3, "parameters = continuationParameters.SearchParameters;")
            .CloseBracket(2)
            .AppendLine()
            // declare orderbys
            .AppendLineWithIndent(2, "var orderBys = continuationParameters?.OrderBys ??")
            .AppendLineWithIndent(2, "[")
            .AppendLineWithIndent(3,
                "$\"{" +
                $"{classSummaryModel.BaseNamespace}.Common.Database.Utility.DatabaseUtility.QuoteResource(nameof({classSummaryModel.FullDtoName}.{classSummaryModel.UpdatedAt.Name}))" +
                "} DESC\"")
            .AppendLineWithIndent(2, "];")
            .AppendLine()
            // Declare queryBuilder
            .AppendLineWithIndent(2, "var queryBuilder = new Dapper.SqlBuilder();")
            .AppendLine()
            // Apply WHERE filters
            .AppendLineWithIndent(2, "/** Apply WHERE filters **/")
            .AppendLineWithIndent(2, "queryBuilder = SetupQueryBuilder(queryBuilder, parameters);")
            .AppendLine()
            // Apply OrderBys
            .AppendLineWithIndent(2, "/** Apply OrderBys filters **/")
            .AppendLineWithIndent(2, "foreach (var orderBy in orderBys){queryBuilder = queryBuilder.OrderBy(orderBy);}")
            .AppendLine()
            // Apply ContinuationToken filtering
            .AppendLineWithIndent(2, "if (continuationParameters is not null)")
            .OpenBracket(2)
            .AppendLineWithIndent(3, "queryBuilder = queryBuilder.Where(")
            .AppendLineWithIndent(4, "$\"{"
                                     + $"{classSummaryModel.BaseNamespace}.Common.Database.Utility.DatabaseUtility.QuoteResource(nameof({classSummaryModel.FullDtoName}.{classSummaryModel.UpdatedAt.Name}))"
                                     + "} <= @checkpoint AND {"
                                     + $"{classSummaryModel.BaseNamespace}.Common.Database.Utility.DatabaseUtility.QuoteResource(nameof({classSummaryModel.FullDtoName}.{classSummaryModel.Key.Name}))"
                                     + "} != @id\",")
            .AppendLineWithIndent(4, "new")
            .OpenBracket(4)
            .AppendLineWithIndent(5, $"checkpoint = continuationParameters.Last{classSummaryModel.UpdatedAt.Name},")
            .AppendLineWithIndent(5, $"id = continuationParameters.Last{classSummaryModel.Key.Name}")
            .AppendLineWithIndent(4, "});")
            .CloseBracket(2)
            .AppendLine()
            // Set page size
            .AppendLineWithIndent(2, "/* Set Page Size */")
            .AppendLineWithIndent(2, "var pageSize = parameters.PageSize;")
            .AppendLineWithIndent(2, "if (pageSize <= 0){pageSize = MaxPageSize;}")
            // Page size cap
            .AppendLineWithIndent(2, "pageSize = Math.Min(MaxPageSize, pageSize); // Cap at programmed max")
            .AppendLine()
            // Declare SQL command
            .AppendLineWithIndent(2, "/* Declare SQL Command */")
            .AppendLineWithIndent(2, "var @params = new { paramTake = pageSize };")
            .AppendLineWithIndent(2, "var template = queryBuilder.AddTemplate($\"SELECT * FROM {"
                                     + $"{classSummaryModel.BaseNamespace}.Common.Database.Utility.DatabaseUtility.QuoteResource(genericDtoStorage.GetTableName())"
                                     + "} /**where**/ /**orderby**/ LIMIT @paramTake\", @params);")
            // Declare and act on SQL connection
            .AppendLineWithIndent(2, "using var dbConnection = await sqlConnectionFactory.GetConnectionAsync();")
            .AppendLineWithIndent(2,
                $"var policy = {classSummaryModel.BaseNamespace}.Common.Database.Utility.PolicyHelper.GetRetryPolicy(logger);")
            .AppendLineWithIndent(2,
                "// Note: Generated code needs to use extension method directly as we aren't importing any using directives")
            .AppendLineWithIndent(2,
                $"var response = await policy.ExecuteAsync(() => Dapper.SqlMapper.QueryAsync<{classSummaryModel.FullDtoName}>(dbConnection, sql: template.RawSql, param: template.Parameters));")
            .AppendLineWithIndent(2, "var records = response.ToList();")
            .AppendLine()
            // Store continuation parameters
            .AppendLineWithIndent(2, "// Hijack continuation token variable")
            .AppendLineWithIndent(2, "continuationToken = records.Count >= pageSize ? Guid.NewGuid() : null;")
            .AppendLineWithIndent(2, "if (continuationToken.HasValue)")
            .OpenBracket(2)
            .AppendLineWithIndent(3, "// Reason to suspect that there's more to get")
            .AppendLineWithIndent(3, "var lastRecord = records[^1]; // Last element")
            .AppendLineWithIndent(3, "await cacheService.SetAsync(continuationToken.Value, new ContinuationParameters")
            .OpenBracket(3)
            .AppendLineWithIndent(4, "OrderBys = orderBys,")
            .AppendLineWithIndent(4, "SearchParameters = parameters,")
            .AppendLineWithIndent(4, $"Last{classSummaryModel.Key.Name} = lastRecord.{classSummaryModel.Key.Name},")
            .AppendLineWithIndent(4,
                $"Last{classSummaryModel.UpdatedAt.Name} = lastRecord.{classSummaryModel.UpdatedAt.Name}")
            .AppendLineWithIndent(3, "}, cancellationToken);")
            .CloseBracket(2)
            // Send response
            .AppendLineWithIndent(2,
                $"return new {classSummaryModel.ResponseClassSearch}")
            .OpenBracket(2)
            .AppendLineWithIndent(3, "Records = records,")
            .AppendLineWithIndent(3, "ContinuationToken = continuationToken")
            .AppendLineWithIndent(2, "};")
            .CloseBracket();
    }

    private static StringBuilder AddContinuationParametersClass(this StringBuilder sb,
        ClassSummaryModel classSummaryModel)
    {
        return sb.AppendLineWithIndent("private sealed class ContinuationParameters")
            .OpenBracket()
            .AppendLineWithIndent(2, "public required List<string> OrderBys { get; init; }")
            .AppendLineWithIndent(2,
                $"public required {classSummaryModel.RequestClassSearch} SearchParameters " +
                "{ get; init; }")
            .AppendLineWithIndent(2,
                $"public required {typeof(Guid).FullName} Last{classSummaryModel.Key.Name} " + "{ get; init; }")
            .AppendLineWithIndent(2,
                $"public required {typeof(DateTime).FullName} Last{classSummaryModel.UpdatedAt.Name} " +
                "{ get; init; }")
            .CloseBracket();
    }

    /// <summary>
    ///     Add generic CRUD through Generic DTO Storage
    ///     We could eventually cut GenericDtoStorage out of the loop here in the generator, but it will never be completely
    ///     obsolete,
    ///     as surely some storage classes will still need bespoke implementations of core/SQL storage (e.g. Icons).
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="classSummaryModel"></param>
    /// <returns></returns>
    private static StringBuilder AddGenericCrud(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        return sb.AppendLine()
            // Delete
            .AppendLineWithIndent(
                $"public {Helper.Taskify("bool")} DeleteAsync({typeof(Guid).FullName} id, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent(2, "return genericDtoStorage.DeleteByKeyAsync(id, cancellationToken);")
            .CloseBracket()
            .AppendLine()
            // GetById
            .AppendLineWithIndent(
                $"public {Helper.Taskify(classSummaryModel.FullDtoName + "?")} GetByIdAsync({typeof(Guid).FullName} id, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent(2, "return genericDtoStorage.GetByKeyAsync(id, cancellationToken);")
            .CloseBracket()
            .AppendLine()
            // Upsert
            .AppendLineWithIndent(
                $"public {Helper.Taskify(classSummaryModel.FullDtoName)} UpsertAsync({classSummaryModel.FullDtoName} item, {typeof(CancellationToken).FullName} cancellationToken = default)")
            .OpenBracket()
            .AppendLineWithIndent(2, "return genericDtoStorage.UpsertAsync(item, cancellationToken);")
            .CloseBracket();
    }

    private static StringBuilder AddWhereQueries(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        var baseNamespace = classSummaryModel.BaseNamespace; // shorthand
        /* Setup Query Builder. Most WHERE statements live here. */
        sb.AppendLine()
            .AppendLineWithIndent(
                $"private static Dapper.SqlBuilder SetupQueryBuilder(Dapper.SqlBuilder builder, {classSummaryModel.RequestClassSearch} parameters)")
            .OpenBracket();

        var propertiesToSearch = classSummaryModel.Properties.ToList();
        if (classSummaryModel.SearchableByKey)
        {
            propertiesToSearch.Add(classSummaryModel.Key);
        }

        // Start iterating through properties
        foreach (var dtoProperty in propertiesToSearch.OrderByDescending(p => p.FilterPriorityWeight))
        {
            if (dtoProperty.CannotFilterBy)
            {
                // Skip
                continue;
            }

            sb.AppendLineWithIndent(2, $"/** Examining Property: {dtoProperty.Name} **/");

            var paramName = dtoProperty.Name.ToLower();

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (dtoProperty.Category)
            {
                case PropertyModel.PropertyCategory.Bool:

                    if (dtoProperty.IsNullable)
                    {
                        throw new NullableSearchNotSupportedException();
                    }

                    sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}.HasValue)")
                        .OpenBracket(2)
                        .AppendLineWithIndent(3, "builder = builder.Where(")
                        .AppendLineWithIndent(4,
                            WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                dtoProperty.Name, $"= @{paramName}") + ",")
                        .AppendLineWithIndent(4,
                            "new {" + paramName + $" = parameters.{dtoProperty.Name}.Value" + "}")
                        .AppendLineWithIndent(3, ");")
                        .CloseBracket(2);
                    break;
                case PropertyModel.PropertyCategory.Guid:
                case PropertyModel.PropertyCategory.Enum:
                    // both guid and enum handling are flat equals comparisons only

                    if (dtoProperty.IsRangeSearchable)
                    {
                        // Multiple
                        if (!dtoProperty.IsNullable)
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}Range.Length > 0)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"IN @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}Range" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                        else
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}Range)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapCheckNullDatabaseUtilityQuoteString(baseNamespace,
                                        classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"IN @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}Range" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                    }
                    else
                    {
                        // Singular
                        if (!dtoProperty.IsNullable)
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}.HasValue)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"= @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}.Value" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                        else
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}.HasValue)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapCheckNullDatabaseUtilityQuoteString(baseNamespace,
                                        classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"= @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}.Value" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                    }

                    break;
                case PropertyModel.PropertyCategory.Integer:
                case PropertyModel.PropertyCategory.FloatingPointNumber:
                    /* Equals (only for integers) */

                    if (dtoProperty.Category == PropertyModel.PropertyCategory.Integer)
                    {
                        if (!dtoProperty.IsNullable)
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}.HasValue)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"= @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}.Value" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                        else
                        {
                            sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}.HasValue)")
                                .OpenBracket(2)
                                .AppendLineWithIndent(3, "builder = builder.Where(")
                                .AppendLineWithIndent(4,
                                    WrapCheckNullDatabaseUtilityQuoteString(baseNamespace,
                                        classSummaryModel.FullDtoName,
                                        dtoProperty.Name, $"= @{paramName}") + ",")
                                .AppendLineWithIndent(4,
                                    "new {" + paramName + $" = parameters.{dtoProperty.Name}.Value" + "}")
                                .AppendLineWithIndent(3, ");")
                                .CloseBracket(2);
                        }
                    }

                    /* Greater Than / Less Than */

                    // Greater Than
                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}GreaterThan.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"> @{paramName}") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $" = parameters.{dtoProperty.Name}GreaterThan.Value" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}GreaterThan.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"> @{paramName}GreaterThan") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $"GreaterThan = parameters.{dtoProperty.Name}GreaterThan.Value" +
                                "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    // Less Than
                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}LessThan.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"< @{paramName}LessThan") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $"LessThan = parameters.{dtoProperty.Name}LessThan.Value" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{dtoProperty.Name}LessThan.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"< @{paramName}LessThan") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $"LessThan = parameters.{dtoProperty.Name}LessThan.Value" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    break;
                case PropertyModel.PropertyCategory.DateTime:

                    /*
                     * Attempt to get a simplified name.
                     * At the moment it's made for properties like 'CreatedAtUtc' and 'UpdatedAtUtc'
                     */
                    var malleableName = Regex.Replace(dtoProperty.Name, "Utc$", string.Empty);
                    var hadUtc = malleableName != dtoProperty.Name;
                    malleableName = Regex.Replace(malleableName, "At$", string.Empty);
                    var restoreUtc = hadUtc ? "Utc" : string.Empty;

                    // Before
                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{malleableName}Before{restoreUtc}.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"< @{malleableName.ToLower()}Before") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + malleableName.ToLower() +
                                $"Before = parameters.{malleableName}Before{restoreUtc}.Value" +
                                "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{malleableName}Before{restoreUtc}.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"< @{malleableName.ToLower()}Before") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + malleableName.ToLower() +
                                $"Before = parameters.{malleableName}Before{restoreUtc}.Value" +
                                "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    // After
                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{malleableName}After{restoreUtc}.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"> @{malleableName.ToLower()}After") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + malleableName.ToLower() +
                                $"After = parameters.{malleableName}After{restoreUtc}.Value" +
                                "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2, $"if(parameters.{malleableName}After{restoreUtc}.HasValue)")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"> @{paramName}After") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $"After = parameters.{malleableName}After{restoreUtc}.Value" +
                                "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    break;
                case PropertyModel.PropertyCategory.String:

                    /* Equals */

                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2, $"if(!string.IsNullOrWhiteSpace(parameters.{dtoProperty.Name}))")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"= @{paramName}") + ",")
                            .AppendLineWithIndent(4, "new {" + paramName + $" = parameters.{dtoProperty.Name}" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2, $"if(!string.IsNullOrWhiteSpace(parameters.{dtoProperty.Name}))")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"= @{paramName}") + ",")
                            .AppendLineWithIndent(4, "new {" + paramName + $" = parameters.{dtoProperty.Name}" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    /* Contains */

                    if (!dtoProperty.IsNullable)
                    {
                        sb.AppendLineWithIndent(2,
                                $"if(!string.IsNullOrWhiteSpace(parameters.{dtoProperty.Name}Contains))")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"LIKE @{paramName}") + ",")
                            .AppendLineWithIndent(4,
                                "new {" + paramName + $" = parameters.{dtoProperty.Name}Contains" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }
                    else
                    {
                        sb.AppendLineWithIndent(2,
                                $"if(!string.IsNullOrWhiteSpace(parameters.{dtoProperty.Name}Contains))")
                            .OpenBracket(2)
                            .AppendLineWithIndent(3, "builder = builder.Where(")
                            .AppendLineWithIndent(4,
                                WrapCheckNullDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                                    dtoProperty.Name, $"LIKE @{paramName}") + ",")
                            .AppendLineWithIndent(4, "new {" + paramName + $" = parameters.{dtoProperty.Name}" + "}")
                            .AppendLineWithIndent(3, ");")
                            .CloseBracket(2);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            } // end switch

            if (dtoProperty.IsNullable)
            {
                sb.AppendLineWithIndent(2,
                        $"if(parameters.{dtoProperty.Name}IsNull)")
                    .OpenBracket(2)
                    .AppendLineWithIndent(3, "builder = builder.Where(")
                    .AppendLineWithIndent(4,
                        WrapSimpleDatabaseUtilityQuoteString(baseNamespace, classSummaryModel.FullDtoName,
                            dtoProperty.Name, "IS NULL"))
                    .AppendLineWithIndent(3, ");")
                    .CloseBracket(2);
            }
        }

        return sb.AppendLineWithIndent(2, "return builder;")
            .CloseBracket(); /* End Setup Query Builder */
    }

    private static string WrapSimpleDatabaseUtilityQuoteString(string baseNamespace, string dtoPath,
        string propertyName, string comparison)
    {
        var subSb = new StringBuilder();
        subSb.Append(
            "$\"{" + baseNamespace + ".Common.Database.Utility.DatabaseUtility.QuoteResource(nameof(" + dtoPath);
        subSb.Append($".{propertyName}))" + "} " + comparison);

        return subSb
            .Append("\"")
            .ToString();
    }

    private static string WrapCheckNullDatabaseUtilityQuoteString(string baseNamespace, string dtoPath,
        string propertyName, string comparison)
    {
        var subSb = new StringBuilder();
        subSb.Append(
            "$\"{" + baseNamespace + ".Common.Database.Utility.DatabaseUtility.QuoteResource(nameof(" + dtoPath);
        subSb.Append($".{propertyName}))" + "} IS NOT NULL");
        subSb.Append(" AND {" + baseNamespace + ".Common.Database.Utility.DatabaseUtility.QuoteResource(nameof(" +
                     dtoPath);
        subSb.Append($".{propertyName}))" + "} " + comparison);

        return subSb
            .Append("\"")
            .ToString();
    }

    public static StringBuilder WriteRepositoryInfo(this StringBuilder sb, ClassSummaryModel classSummaryModel)
    {
        var baseNamespace = classSummaryModel.BaseNamespace; // shorthand
        sb
            .AppendLine()
            .AppendLine($"internal class {classSummaryModel.RepositoryName}(")
            .AppendLineWithIndent($"{baseNamespace}.Common.Cache.Services.ICacheService cacheService,")
            .AppendLineWithIndent(
                $"{baseNamespace}.Common.Database.Services.IGenericDtoStorage<{classSummaryModel.FullDtoName}, {classSummaryModel.Key.Type}> genericDtoStorage,")
            .AppendLineWithIndent(
                $"Microsoft.Extensions.Logging.ILogger<{classSummaryModel.RepositoryName}> logger,")
            .AppendLineWithIndent(
                $"{baseNamespace}.Common.Database.Services.ISqlConnectionFactory sqlConnectionFactory) : {classSummaryModel.RepositoryInterfaceName}")
            .OpenBracket(0)
            .AppendLineWithIndent($"private const int MaxPageSize = {classSummaryModel.MaxSearchPageSize};");

        sb
            .AddWhereQueries(classSummaryModel)
            .AddGenericCrud(classSummaryModel)
            .AddSearchMethod(classSummaryModel)
            .AddContinuationParametersClass(classSummaryModel);

        sb
            .CloseBracket(0); // End class

        return sb;
    }
}