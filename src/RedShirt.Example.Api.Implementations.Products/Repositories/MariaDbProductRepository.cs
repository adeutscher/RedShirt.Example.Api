using Dapper;
using RedShirt.Example.Api.Common.Database.DapperMySql.Factories;
using RedShirt.Example.Api.Common.Database.DapperMySql.Services;
using RedShirt.Example.Api.Common.Database.DapperMySql.Services.Resilience;
using RedShirt.Example.Api.Common.Database.DapperMySql.Utility;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;
using RedShirt.Example.Api.Implementations.Constants;
using RedShirt.Example.Api.Implementations.Products.Models;

namespace RedShirt.Example.Api.Implementations.Products.Repositories;

internal sealed class MariaDbProductRepository(
    IRemoteCacheService cacheService,
    IGenericMySqlDtoStorage<ProductDto, Guid> genericDtoStorage,
    ISqlConnectionFactory sqlConnectionFactory,
    IMySqlRetryWrapperService retryWrapperService) : IProductRepository
{
    private const int MaxPageSize = 100;
    private const string ConnectionStringName = DatabaseConstants.PrimaryDatabaseConnectionStringName;

    private static SqlBuilder SetupQueryBuilder(SqlBuilder builder, ProductSearchParameters parameters)
    {
        if (parameters.CreatedBeforeUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.CreatedAtUtc))} < @createdBefore",
                new {createdBefore = parameters.CreatedBeforeUtc.Value});
        }

        if (parameters.CreatedAfterUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.CreatedAtUtc))} > @createdAfter",
                new {createdAfter = parameters.CreatedAfterUtc.Value});
        }

        if (parameters.UpdatedBeforeUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.UpdatedAtUtc))} < @updatedBefore",
                new {updatedBefore = parameters.UpdatedBeforeUtc.Value});
        }

        if (parameters.UpdatedAfterUtc.HasValue)
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.UpdatedAtUtc))} > @updatedAfter",
                new {updatedAfter = parameters.UpdatedAfterUtc.Value});
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sku))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Sku))} = @sku",
                new {sku = parameters.Sku});
        }

        if (!string.IsNullOrWhiteSpace(parameters.SkuContains))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Sku))} LIKE @sku",
                new {sku = parameters.SkuContains});
        }

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Name))} = @name",
                new {name = parameters.Name});
        }

        if (!string.IsNullOrWhiteSpace(parameters.NameContains))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Name))} LIKE @name",
                new {name = parameters.NameContains});
        }

        if (!string.IsNullOrWhiteSpace(parameters.Price))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Price))} = @price",
                new
                {
                    price = StoredAsDecimalHelper.ParseRequiredDecimal(parameters.Price,
                        nameof(ProductSearchParameters.Price))
                });
        }

        if (!string.IsNullOrWhiteSpace(parameters.PriceGreaterThan))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Price))} > @price",
                new
                {
                    price = StoredAsDecimalHelper.ParseRequiredDecimal(parameters.PriceGreaterThan,
                        nameof(ProductSearchParameters.PriceGreaterThan))
                });
        }

        if (!string.IsNullOrWhiteSpace(parameters.PriceLessThan))
        {
            builder = builder.Where(
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.Price))} < @priceLessThan",
                new
                {
                    priceLessThan = StoredAsDecimalHelper.ParseRequiredDecimal(parameters.PriceLessThan,
                        nameof(ProductSearchParameters.PriceLessThan))
                });
        }

        return builder;
    }

    private static ProductModel ToModel(ProductDto dto)
    {
        return new ProductModel
        {
            Id = dto.Id,
            CreatedAtUtc = dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc,
            Sku = dto.Sku,
            Name = dto.Name,
            Price = dto.Price
        };
    }

    private static ProductDto ToDto(ProductModel model)
    {
        return new ProductDto
        {
            Id = model.Id,
            CreatedAtUtc = model.CreatedAtUtc,
            UpdatedAtUtc = model.UpdatedAtUtc,
            Sku = model.Sku,
            Name = model.Name,
            Price = model.Price
        };
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return genericDtoStorage.DeleteByKeyAsync(ConnectionStringName, id, cancellationToken);
    }

    public async Task<ProductModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await genericDtoStorage.GetByKeyAsync(ConnectionStringName, id, cancellationToken);
        return dto is null ? null : ToModel(dto);
    }

    public async Task<ProductModel> UpsertAsync(ProductModel item, CancellationToken cancellationToken = default)
    {
        var dto = await genericDtoStorage.UpsertAsync(ConnectionStringName, ToDto(item), cancellationToken);
        return ToModel(dto);
    }

    public async Task<ProductListModel> SearchAsync(ProductSearchParameters parameters,
        Guid? continuationToken, CancellationToken cancellationToken = default)
    {
        ContinuationParameters? continuationParameters = null;
        if (continuationToken is not null)
        {
            var continuationParametersKey = $"continuation:{continuationToken.Value}";
            continuationParameters =
                await cacheService.GetObjectAsync<ContinuationParameters>(continuationParametersKey, cancellationToken);
        }

        if (continuationParameters is not null)
        {
            parameters = continuationParameters.SearchParameters;
        }

        var orderBys = continuationParameters?.OrderBys ??
        [
            $"{DatabaseUtility.QuoteResource(nameof(ProductDto.UpdatedAtUtc))} DESC"
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
                $"{DatabaseUtility.QuoteResource(nameof(ProductDto.UpdatedAtUtc))} <= @checkpoint AND {DatabaseUtility.QuoteResource(nameof(ProductDto.Id))} != @id",
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
        var selectList = StoredAsDecimalHelper.BuildSelectClause(typeof(ProductDto));
        var template = queryBuilder.AddTemplate(
            $"SELECT {selectList} FROM {DatabaseUtility.QuoteResource(genericDtoStorage.GetTableName())} /**where**/ /**orderby**/ LIMIT @paramTake",
            @params);

        using var dbConnection =
            await sqlConnectionFactory.GetMySqlConnectionAsync(ConnectionStringName, cancellationToken);
        var response = await retryWrapperService.RunAsync(
            _ => dbConnection.QueryAsync<ProductDto>(template.RawSql, template.Parameters),
            cancellationToken);
        var records = response.ToList();

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

        return new ProductListModel
        {
            Items = records.Select(ToModel).ToList(),
            ContinuationToken = continuationToken
        };
    }

    private sealed class ContinuationParameters
    {
        public required List<string> OrderBys { get; init; }
        public required ProductSearchParameters SearchParameters { get; init; }
        public required Guid LastId { get; init; }
        public required DateTime LastUpdatedAtUtc { get; init; }
    }
}
