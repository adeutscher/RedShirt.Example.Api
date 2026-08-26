using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Update;

public interface IUpdateCustomerCommandHandler : ICqrsHandler<UpdateCustomerCommand, CustomerDto>;

internal class UpdateCustomerCommandHandler(
    ICustomerService customerService,
    ICoreRequestValidator coreRequestValidator)
    : IUpdateCustomerCommandHandler
{
    public async Task<CustomerDto> Handle(UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await customerService.PutAsync(new CustomerServicePutRequest
        {
            Id = command.Id,
            Email = command.Email,
            DisplayName = command.DisplayName
        }, cancellationToken);
    }
}
