using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;

public interface IUpdateOrderCommandHandler : ICqrsHandler<UpdateOrderCommand, OrderDto>;

internal class UpdateOrderCommandHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IUpdateOrderCommandHandler
{
    public async Task<OrderDto> Handle(UpdateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await orderService.PutAsync(new OrderServicePutRequest
        {
            Id = command.Id,
            CustomerId = command.CustomerId,
            Status = command.Status,
            TotalAmount = command.TotalAmount
        }, cancellationToken);
    }
}