using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Patch;

public interface IPatchCustomerCommandHandler : ICqrsHandler<PatchCustomerCommand, CustomerDto>;

internal class PatchCustomerCommandHandler(
    ICustomerService customerService,
    ICoreRequestValidator coreRequestValidator)
    : IPatchCustomerCommandHandler
{
    public async Task<CustomerDto> Handle(PatchCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await customerService.PatchAsync(new CustomerServicePatchRequest
        {
            Id = command.Id,
            Email = command.Email,
            DisplayName = command.DisplayName
        }, cancellationToken);
    }
}