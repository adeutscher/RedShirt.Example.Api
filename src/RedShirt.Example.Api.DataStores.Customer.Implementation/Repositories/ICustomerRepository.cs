using RedShirt.Example.Api.DataStores.Customer.Core.Models;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Repositories;

internal interface ICustomerRepository
{
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerSearchResponse> SearchAsync(CustomerServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> UpsertAsync(CustomerDto item, CancellationToken cancellationToken = default);
}