using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Queries.GetRecord;

public interface IGetCustomerRecordQueryHandler : ICqrsHandler<GetCustomerRecordQuery, CustomerDto>;

internal class GetCustomerRecordQueryHandler(
    ICustomerService customerService,
    ICoreRequestValidator coreRequestValidator)
    : IGetCustomerRecordQueryHandler
{
    public async Task<CustomerDto> Handle(GetCustomerRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await customerService.GetByIdAsync(query.Id, cancellationToken);
    }
}
