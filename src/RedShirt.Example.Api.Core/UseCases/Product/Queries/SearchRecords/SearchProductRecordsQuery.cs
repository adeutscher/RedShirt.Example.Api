using RedShirt.Example.Api.Core.UseCases.Product.Models;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public record SearchProductRecordsQuery(ProductSearchParameters Parameters, Guid? ContinuationToken);
