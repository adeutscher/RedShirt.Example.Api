using Dapper;
using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Common.Database.DapperMySql.Exceptions;
using RedShirt.Example.Api.Common.Database.DapperMySql.Factories;
using RedShirt.Example.Api.Common.Database.DapperMySql.Services.Resilience;
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
    IMySqlRetryWrapperService retryWrapperService)
    : IGenericMySqlDtoStorage<TDto, TKey> where TDto : class

{
    private static IEnumerable<PropertyInfo> GetMappedProperties(bool excludeKey = false)
    {
        var keyField = excludeKey ? string.Empty : GetKeyFieldName();

        return typeof(TDto).GetProperties()
            .Where(p => !excludeKey || !keyField.Equals(p.Name));
    }

    private async Task<TDto> InsertAsync(IDbConnection dbConnection, TDto itemTemplate,
        CancellationToken cancellationToken = default)
    {
        var properties = GetMappedProperties().ToList();

        var fieldFields = string.Join(",",
            properties.Select(p => DatabaseUtility.QuoteResource(StoredAsDecimalHelper.GetColumnName(p))));
        var valueFields = string.Join(",", properties.Select(p => $"@{p.Name}"));

        var query = $"INSERT INTO `{GetTableName()}` ({fieldFields}) VALUES ({valueFields});";

        await retryWrapperService.RunAsync(
            _ => dbConnection.ExecuteAsync(query, StoredAsDecimalHelper.ToWriteParameters(itemTemplate)),
            cancellationToken);

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
            var columnName = StoredAsDecimalHelper.GetColumnName(property);
            builder.Set($"{DatabaseUtility.QuoteResource(columnName)}=@{property.Name}");
        }

        builder.Where($"{DatabaseUtility.QuoteResource(keyName)}=@key");

        if (!changed)
        {
            throw new NoChangesToModifyException();
        }

        var template = builder.AddTemplate($"UPDATE `{GetTableName()}` /**set**/ /**where**/");
        var parameters = StoredAsDecimalHelper.ToWriteParameters(newDto);
        parameters.Add("key", GetKeyFieldValue(oldDto));

        await retryWrapperService.RunAsync(_ => dbConnection.ExecuteAsync(template.RawSql, parameters),
            cancellationToken);

        return (await GetByKeyAsync(dbConnection, GetKeyFieldValue(newDto), cancellationToken))!;
    }

    private static string GetKeyFieldName()
    {
        return GetKeyProperty().Name;
    }

    private async Task<TDto?> GetByKeyAsync(IDbConnection dbConnection, TKey entryId,
        CancellationToken cancellationToken = default)
    {
        var selectList = StoredAsDecimalHelper.BuildSelectClause(typeof(TDto));
        var query =
            $"SELECT {selectList} FROM `{GetTableName()}` WHERE `{GetKeyFieldName()}` = @entryId";
        var response = await retryWrapperService.RunAsync(_ => dbConnection.QueryFirstOrDefaultAsync<TDto>(query, new
        {
            entryId
        }), cancellationToken);

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
        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(connectionStringName, cancellationToken);

        var builder = new SqlBuilder();
        builder.Where($"{GetKeyFieldName()}=@key", new {key});
        var template = builder.AddTemplate($"DELETE FROM `{GetTableName()}` /**where**/");
        var result =
            await retryWrapperService.RunAsync(_ => dbConnection.ExecuteAsync(template.RawSql, template.Parameters),
                cancellationToken);
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