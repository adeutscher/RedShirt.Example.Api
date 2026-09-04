using Dapper;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Factories;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Services;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Services.Resilience;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Utility;
using RedShirt.Example.Api.DataStores.Constants;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Implementation.Entities;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Repositories;

internal sealed class MariaDbProductRepository(
    IRemoteCacheService cacheService,
    IGenericMySqlDtoStorage<ProductEntity, Guid> genericDtoStorage,
    ISqlConnectionFactory sqlConnectionFactory,
    IMySqlRetryWrapperService retryWrapperService) : IProductRepository
{
    private const int MaxPageSize = 100;
    private const string ConnectionStringName = DatabaseConstants.PrimaryDatabaseConnectionStringName;

    private static ProductInternalDto ToInternalDto(ProductEntity entity)
    {
        return new ProductInternalDto
        {
            Id = entity.Id,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Sku = entity.Sku,
            Name = entity.Name,
            Price = entity.Price
        };
    }

    private static ProductEntity ToEntity(ProductInternalDto dto)
    {
        return new ProductEntity
        {
            Id = dto.Id,
            CreatedAtUtc = dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc,
            Sku = dto.Sku,
            Name = dto.Name,
            Price = dto.Price
        };
    }

    private static SqlBuilder SetupQueryBuilder(SqlBuilder builder, ProductServiceSearchRequest parameters)
    {
        if (parameters.CreatedBeforeUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.CreatedAtUtc))} < @createdBefore",
                new {createdBefore = parameters.CreatedBeforeUtc.Value});
        }

        if (parameters.CreatedAfterUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.CreatedAtUtc))} > @createdAfter",
                new {createdAfter = parameters.CreatedAfterUtc.Value});
        }

        if (parameters.UpdatedBeforeUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} < @updatedBefore",
                new {updatedBefore = parameters.UpdatedBeforeUtc.Value});
        }

        if (parameters.UpdatedAfterUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} > @updatedAfter",
                new {updatedAfter = parameters.UpdatedAfterUtc.Value});
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sku))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Sku))} = @sku",
                new {sku = parameters.Sku});
        }

        if (!string.IsNullOrWhiteSpace(parameters.SkuContains))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Sku))} LIKE @sku",
                new {sku = parameters.SkuContains});
        }

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Name))} = @name",
                new {name = parameters.Name});
        }

        if (!string.IsNullOrWhiteSpace(parameters.NameContains))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Name))} LIKE @name",
                new {name = parameters.NameContains});
        }

        if (parameters.Price.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Price))} = @price",
                new {price = parameters.Price.Value});
        }

        if (parameters.PriceGreaterThan.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Price))} > @price",
                new {price = parameters.PriceGreaterThan.Value});
        }

        if (parameters.PriceLessThan.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Price))} < @priceLessThan",
                new {priceLessThan = parameters.PriceLessThan.Value});
        }

        return builder;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return genericDtoStorage.DeleteByKeyAsync(ConnectionStringName, id,
            cancellationToken);
    }

    public async Task<ProductInternalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await genericDtoStorage.GetByKeyAsync(ConnectionStringName, id,
            cancellationToken);
        return entity is null ? null : ToInternalDto(entity);
    }

    public async Task<ProductInternalDto> UpsertAsync(ProductInternalDto item,
        CancellationToken cancellationToken = default)
    {
        var entity = await genericDtoStorage.UpsertAsync(ConnectionStringName,
            ToEntity(item), cancellationToken);
        return ToInternalDto(entity);
    }

    public async Task<ProductServiceSearchResponse> SearchAsync(ProductServiceSearchRequest parameters,
        Guid? continuationToken, CancellationToken cancellationToken = default)
    {
        ContinuationParameters? continuationParameters = null;
        if (continuationToken is not null)
        {
            var continuationParametersKey = $"continuation:{continuationToken.Value}";
            continuationParameters =
                await cacheService.GetObjectAsync<ContinuationParameters>(continuationParametersKey,
                    cancellationToken);
        }

        if (continuationParameters is not null)
        {
            parameters = continuationParameters.SearchParameters;
        }

        var orderBys = continuationParameters?.OrderBys ??
        [
            $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} DESC"
        ];

        var queryBuilder = new SqlBuilder();
        queryBuilder = SetupQueryBuilder(queryBuilder, parameters);

        foreach (var orderBy in orderBys)
        {
            queryBuilder = queryBuilder.OrderBy(orderBy);
        }

        if (continuationParameters is not null)
        {
            queryBuilder = queryBuilder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} <= @checkpoint AND {DatabaseUtility.QuoteResource(nameof(ProductEntity.Id))} != @id",
                new
                {
                    checkpoint = continuationParameters.LastUpdatedAtUtc,
                    id = continuationParameters.LastId
                });
        }

        var pageSize = parameters.PageSize;
        if (pageSize <= 0)
        {
            pageSize = MaxPageSize;
        }

        pageSize = Math.Min(MaxPageSize, pageSize);

        var @params = new {paramTake = pageSize};
        var selectList = StoredAsDecimalHelper.BuildSelectClause(typeof(ProductEntity));
        var template = queryBuilder.AddTemplate(
            $"SELECT {selectList} FROM {DatabaseUtility.QuoteResource(genericDtoStorage.GetTableName())} /**where**/ /**orderby**/ LIMIT @paramTake",
            @params);

        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(ConnectionStringName,
                cancellationToken);
        var response = await retryWrapperService.RunAsync(
            _ => dbConnection.QueryAsync<ProductEntity>(template.RawSql, template.Parameters),
            cancellationToken);
        var records = response.Select(ToInternalDto).ToList();

        continuationToken = records.Count >= pageSize ? Guid.NewGuid() : null;
        if (continuationToken.HasValue)
        {
            var lastRecord = records[^1];
            await cacheService.SetObjectAsync(
                $"continuation:{continuationToken.Value}",
                new ContinuationParameters
                {
                    OrderBys = orderBys,
                    SearchParameters = parameters,
                    LastId = lastRecord.Id,
                    LastUpdatedAtUtc = lastRecord.UpdatedAtUtc
                },
                TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        return new ProductServiceSearchResponse
        {
            Records = records,
            ContinuationToken = continuationToken
        };
    }

    private sealed class ContinuationParameters
    {
        public required List<string> OrderBys { get; init; }
        public required ProductServiceSearchRequest SearchParameters { get; init; }
        public required Guid LastId { get; init; }
        public required DateTime LastUpdatedAtUtc { get; init; }
    }
}