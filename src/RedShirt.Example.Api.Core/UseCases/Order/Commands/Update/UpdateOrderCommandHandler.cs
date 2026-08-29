using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;

public interface IUpdateOrderCommandHandler : ICqrsHandler<UpdateOrderCommand, OrderDto>;

internal class UpdateOrderCommandHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IUpdateOrderCommandHandler
{
    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task<OrderDto> Handle(UpdateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return (await orderService.PutAsync(new OrderServicePutRequest
        {
            Id = command.Id,
            CustomerId = command.CustomerId,
            Status = command.Status,
            TotalAmount = decimal.Parse(command.TotalAmount, CultureInfo.InvariantCulture),
            TotalPrice = ParseOptionalDecimal(command.TotalPrice)
        }, cancellationToken)).ToDto();
    }
}