using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Delete;

public interface IDeleteCustomerCommandHandler : ICqrsHandler<DeleteCustomerCommand>;

internal class DeleteCustomerCommandHandler(
    ICustomerService customerService,
    ICoreRequestValidator coreRequestValidator)
    : IDeleteCustomerCommandHandler
{
    public async Task Handle(DeleteCustomerCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await customerService.DeleteAsync(command.Id, cancellationToken);
    }
}
