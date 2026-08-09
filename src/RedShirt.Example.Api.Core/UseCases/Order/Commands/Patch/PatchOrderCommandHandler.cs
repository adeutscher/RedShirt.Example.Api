using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;

public interface IPatchOrderCommandHandler : ICqrsHandler<PatchOrderCommand, OrderDto>;

internal class PatchOrderCommandHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IPatchOrderCommandHandler
{
    public async Task<OrderDto> Handle(PatchOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await orderService.PatchAsync(new OrderServicePatchRequest
        {
            Id = command.Id,
            CustomerId = command.CustomerId,
            Status = command.Status,
            TotalAmount = command.TotalAmount
        }, cancellationToken);
    }
}
