using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Create;

public interface ICreateOrderCommandHandler : ICqrsHandler<CreateOrderCommand, OrderDto>;

internal class CreateOrderCommandHandler(
    IOrderService orderService,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateOrderCommandHandler
{
    public async Task<OrderDto> Handle(CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
            await orderService.PostAsync(new OrderServicePostRequest
            {
                CustomerId = command.CustomerId,
                Status = command.Status,
                TotalAmount = command.TotalAmount
            }, cancellationToken), cancellationToken);
    }
}
