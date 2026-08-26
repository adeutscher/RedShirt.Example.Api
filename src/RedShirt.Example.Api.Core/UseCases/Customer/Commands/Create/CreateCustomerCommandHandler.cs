using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Create;

public interface ICreateCustomerCommandHandler : ICqrsHandler<CreateCustomerCommand, CustomerDto>;

internal class CreateCustomerCommandHandler(
    ICustomerService customerService,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateCustomerCommandHandler
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
            await customerService.PostAsync(new CustomerServicePostRequest
            {
                Email = command.Email,
                DisplayName = command.DisplayName
            }, cancellationToken), cancellationToken);
    }
}