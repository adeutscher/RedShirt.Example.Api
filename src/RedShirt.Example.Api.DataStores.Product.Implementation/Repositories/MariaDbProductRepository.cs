using Dapper;
using RedShirt.Example.Api.Common.Distributed.Continuation;
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

    private const string ContinuationEntity = "product";

    private static ProductDto ToDto(ProductEntity entity)
    {
        return new ProductDto
        {
            Id = entity.Id,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Sku = entity.Sku,
            Name = entity.Name,
            Price = entity.Price
        };
    }

    private static ProductEntity ToEntity(ProductDto dto)
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
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Sku))} LIKE @skuContains {SqlQueryHelper.LikeEscapeClause}",
                new {skuContains = SqlQueryHelper.EncodeTermForLike(parameters.SkuContains)});
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
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Name))} LIKE @nameContains {SqlQueryHelper.LikeEscapeClause}",
                new {nameContains = SqlQueryHelper.EncodeTermForLike(parameters.NameContains)});
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
                $"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Price))} > @priceGreaterThan",
                new {priceGreaterThan = parameters.PriceGreaterThan.Value});
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

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await genericDtoStorage.GetByKeyAsync(ConnectionStringName, id,
            cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ProductDto> UpsertAsync(ProductDto item,
        CancellationToken cancellationToken = default)
    {
        var entity = await genericDtoStorage.UpsertAsync(ConnectionStringName,
            ToEntity(item), cancellationToken);
        return ToDto(entity);
    }

    public async Task<ProductServiceSearchResponse> SearchAsync(ProductServiceSearchRequest parameters,
        Guid? continuationToken, CancellationToken cancellationToken = default)
    {
        ContinuationParameters? continuationParameters = null;
        if (continuationToken is not null)
        {
            var continuationParametersKey =
                ContinuationTokenScope.CacheKey(ContinuationEntity, continuationToken.Value);
            continuationParameters =
                await cacheService.GetObjectAsync<ContinuationParameters>(continuationParametersKey,
                    cancellationToken);
        }

        if (continuationParameters is not null
            && !ContinuationTokenScope.IsCompatible(parameters, continuationParameters.SearchParameters,
                nameof(ProductServiceSearchRequest.PageSize)))
        {
            // Contradicts the caller-scoped request (another caller's token, or changed filters).
            // Treat it like an expired token and start from the first page.
            continuationParameters = null;
        }

        if (continuationParameters is not null)
        {
            parameters = continuationParameters.SearchParameters;
        }

        var queryBuilder = new SqlBuilder();
        queryBuilder = SetupQueryBuilder(queryBuilder, parameters);

        // Always built here, never read from the cache: the keyset condition below depends on this exact order, and
        // SQL text read back from Redis would let anyone who can write to the cache inject SQL.
        queryBuilder = queryBuilder
            .OrderBy($"{DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} DESC")
            // The key breaks ties between records updated at the same instant, so pages have a total order.
            .OrderBy($"{DatabaseUtility.QuoteResource(nameof(ProductEntity.Id))} DESC");

        if (continuationParameters is not null)
        {
            // Keyset continuation on (UpdatedAtUtc, Id): strictly after the last record in the DESC order.
            queryBuilder = queryBuilder.Where(
                $"({DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} < @checkpoint OR ({DatabaseUtility.QuoteResource(nameof(ProductEntity.UpdatedAtUtc))} = @checkpoint AND {DatabaseUtility.QuoteResource(nameof(ProductEntity.Id))} < @id))",
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
        var selectList = DapperDtoSqlHelper.BuildSelectClause(typeof(ProductEntity));
        var template = queryBuilder.AddTemplate(
            $"SELECT {selectList} FROM {DatabaseUtility.QuoteResource(genericDtoStorage.GetTableName())} /**where**/ /**orderby**/ LIMIT @paramTake",
            @params);

        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(ConnectionStringName,
                cancellationToken);
        var response = await retryWrapperService.RunAsync(
            _ => dbConnection.QueryAsync<ProductEntity>(template.RawSql, template.Parameters),
            cancellationToken);
        var records = response.Select(ToDto).ToList();

        continuationToken = records.Count >= pageSize ? Guid.NewGuid() : null;
        if (continuationToken.HasValue)
        {
            var lastRecord = records[^1];
            await cacheService.SetObjectAsync(
                ContinuationTokenScope.CacheKey(ContinuationEntity, continuationToken.Value),
                new ContinuationParameters
                {
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
        public required ProductServiceSearchRequest SearchParameters { get; init; }
        public required Guid LastId { get; init; }
        public required DateTime LastUpdatedAtUtc { get; init; }
    }
}