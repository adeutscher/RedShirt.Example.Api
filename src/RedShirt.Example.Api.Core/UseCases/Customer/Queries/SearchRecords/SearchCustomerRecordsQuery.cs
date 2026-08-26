using RedShirt.Example.Api.DataStores.Customer.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Queries.SearchRecords;

public record SearchCustomerRecordsQuery(CustomerServiceSearchRequest Parameters, Guid? ContinuationToken);
