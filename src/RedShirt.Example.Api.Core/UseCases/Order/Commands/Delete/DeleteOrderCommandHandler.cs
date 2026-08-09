using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Delete;

public interface IDeleteOrderCommandHandler : ICqrsHandler<DeleteOrderCommand>;

internal class DeleteOrderCommandHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IDeleteOrderCommandHandler
{
    public async Task Handle(DeleteOrderCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await orderService.DeleteAsync(command.Id, cancellationToken);
    }
}