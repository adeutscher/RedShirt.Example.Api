using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Queries.SearchRecords;

public interface ISearchCustomerRecordsQueryHandler : ICqrsHandler<SearchCustomerRecordsQuery, CustomerSearchResponse>;

internal class SearchCustomerRecordsQueryHandler(ICustomerService customerService)
    : ISearchCustomerRecordsQueryHandler
{
    public Task<CustomerSearchResponse> Handle(SearchCustomerRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        return customerService.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
    }
}