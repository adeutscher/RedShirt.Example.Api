using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public record SearchOrderRecordsQuery(OrderServiceSearchRequest Parameters, Guid? ContinuationToken);
