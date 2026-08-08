using Dapper;
using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Common.Database.DapperMySql.Exceptions;
using RedShirt.Example.Api.Common.Database.DapperMySql.Factories;
using RedShirt.Example.Api.Common.Database.DapperMySql.Utility;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Services;

public interface IGenericMySqlDtoStorage<TDto, in TKey> where TDto : class
{
    Task<bool> DeleteByKeyAsync(string connectionStringName, TKey key, CancellationToken cancellationToken = default);
    Task<TDto?> GetByKeyAsync(string connectionStringName, TKey entryId, CancellationToken cancellationToken = default);

    string GetTableName();

    Task<TDto> UpsertAsync(string connectionStringName, TDto dto,
        CancellationToken cancellationToken = default);
}

public class GenericMySqlDtoStorage<TDto, TKey>(
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<GenericMySqlDtoStorage<TDto, TKey>> logger)
    : IGenericMySqlDtoStorage<TDto, TKey> where TDto : class

{
    private IEnumerable<string> GetFieldNames(bool excludeKey = false)
    {
        var keyField = excludeKey ? string.Empty : GetKeyFieldName(); // Fetch once, if needed at all

        return typeof(TDto).GetProperties()
            .Where(p => !excludeKey || !keyField.Equals(p.Name))
            .Select(p =>
            {
                var columnAttribute = p.GetCustomAttribute<ColumnAttribute>();
                if (columnAttribute is not null && !string.IsNullOrWhiteSpace(columnAttribute.Name))
                {
                    // Return name as specified in Column attribute
                    return columnAttribute.Name;
                }

                return p.Name;
            });
    }

    private async Task<TDto> InsertAsync(IDbConnection dbConnection, TDto itemTemplate,
        CancellationToken cancellationToken = default)
    {
        var policy = PolicyHelper.GetRetryPolicy(logger);

        var fields = GetFieldNames().ToList();

        var fieldFields = string.Join(",", fields.Select(f => $"`{f}`"));
        var valueFields = string.Join(",", fields.Select(f => $"@{f}"));

        var query = $"INSERT INTO `{GetTableName()}` ({fieldFields}) VALUES ({valueFields});";

        await policy.ExecuteAsync(() => dbConnection.ExecuteAsync(query, itemTemplate));

        return itemTemplate;
    }

    private async Task<TDto> UpdateAsync(IDbConnection dbConnection, TDto oldDto,
        TDto newDto, CancellationToken cancellationToken)
    {
        var keyName = GetKeyFieldName();

        var changed = false;
        var builder = new SqlBuilder();

        foreach (var property in typeof(TDto).GetProperties())
        {
            if (property.Name == keyName)
            {
                continue;
            }

            var oldValue = property.GetValue(oldDto);
            var newValue = property.GetValue(newDto);

            if ((oldValue is null && newValue is null) || (oldValue?.Equals(newValue) ?? true))
            {
                continue;
            }

            changed = true;
            builder.Set($"{property.Name}=@{property.Name}", newDto);
        }

        builder.Where($"{keyName}=@key", new
        {
            key = GetKeyFieldValue(oldDto)
        });

        if (!changed)
        {
            throw new NoChangesToModifyException();
        }

        var policy = PolicyHelper.GetRetryPolicy(logger);
        var template = builder.AddTemplate($"UPDATE `{GetTableName()}` /**set**/ /**where**/");
        await policy.ExecuteAsync(() => dbConnection.ExecuteAsync(template.RawSql, template.Parameters));

        return (await GetByKeyAsync(dbConnection, GetKeyFieldValue(newDto), cancellationToken))!;
    }

    private static string GetKeyFieldName()
    {
        return GetKeyProperty().Name;
    }

    private async Task<TDto?> GetByKeyAsync(IDbConnection dbConnection, TKey entryId,
        CancellationToken cancellationToken = default)
    {
        var policy = PolicyHelper.GetRetryPolicy(logger);

        var query = $"SELECT * FROM `{GetTableName()}` WHERE `{GetKeyFieldName()}` = @entryId";
        var response = await policy.ExecuteAsync(() => dbConnection.QueryFirstOrDefaultAsync<TDto>(query, new
        {
            entryId
        }));

        return response;
    }

    private static PropertyInfo GetKeyProperty()
    {
        return typeof(TDto).GetProperties()
                   .FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute)).Any())
               ?? typeof(TDto).GetProperties()
                   .FirstOrDefault(p => p.GetCustomAttributes(typeof(DbKeyAttribute)).Any())
               ?? throw new CouldNotLocateKeyException();
    }

    public string GetTableName()
    {
        return typeof(TDto).GetCustomAttributes<TableAttribute>().FirstOrDefault()?.Name
               ?? typeof(TDto).GetCustomAttributes<DbTableAttribute>().FirstOrDefault()?.TableName
               ?? typeof(TDto).Name;
    }

    public async Task<bool> DeleteByKeyAsync(string connectionStringName, TKey key,
        CancellationToken cancellationToken = default)
    {
        var policy = PolicyHelper.GetRetryPolicy(logger);
        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(connectionStringName, cancellationToken);

        var builder = new SqlBuilder();
        builder.Where($"{GetKeyFieldName()}=@key", new {key});
        var template = builder.AddTemplate($"DELETE FROM `{GetTableName()}` /**where**/");
        var result = await policy.ExecuteAsync(() => dbConnection.ExecuteAsync(template.RawSql, template.Parameters));
        return result == 1;
    }

    public async Task<TDto?> GetByKeyAsync(string connectionStringName, TKey entryId,
        CancellationToken cancellationToken = default)
    {
        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(connectionStringName, cancellationToken);
        return await GetByKeyAsync(dbConnection, entryId, cancellationToken);
    }

    public async Task<TDto> UpsertAsync(string connectionStringName,
        TDto dto,
        CancellationToken cancellationToken = default)
    {
        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(connectionStringName, cancellationToken);

        var existingItem = await GetByKeyAsync(dbConnection, GetKeyFieldValue(dto), cancellationToken);
        if (existingItem is null)
        {
            return await InsertAsync(dbConnection, dto, cancellationToken);
        }

        return await UpdateAsync(dbConnection, existingItem, dto, cancellationToken);
    }

    internal static TKey GetKeyFieldValue(TDto dto)
    {
        var keyProperty = GetKeyProperty();

        return (TKey) keyProperty.GetValue(dto)!;
    }
}