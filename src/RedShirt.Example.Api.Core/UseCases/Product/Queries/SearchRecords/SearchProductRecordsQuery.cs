using RedShirt.Example.Api.DataStores.Product.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public record SearchProductRecordsQuery(ProductServiceSearchRequest Parameters, Guid? ContinuationToken);
