using RedShirt.Example.Api.DataStores.Customer.Core.Models;

namespace RedShirt.Example.Api.DataStores.Customer.Core.Services;

public interface ICustomerService
{
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> PatchAsync(CustomerServicePatchRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> PostAsync(CustomerServicePostRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> PutAsync(CustomerServicePutRequest request, CancellationToken cancellationToken = default);

    Task<CustomerSearchResponse> SearchAsync(CustomerServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);
}
