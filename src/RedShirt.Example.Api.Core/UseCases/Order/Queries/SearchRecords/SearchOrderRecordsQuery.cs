namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public record SearchOrderRecordsQuery(OrderQuerySearchParameters Parameters, Guid? ContinuationToken);
