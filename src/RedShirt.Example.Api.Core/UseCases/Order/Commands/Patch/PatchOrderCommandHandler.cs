using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;

public interface IPatchOrderCommandHandler : ICqrsHandler<PatchOrderCommand, OrderDto>;

internal class PatchOrderCommandHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IPatchOrderCommandHandler
{
    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task<OrderDto> Handle(PatchOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return (await orderService.PatchAsync(new OrderServicePatchRequest
        {
            Id = command.Id,
            CustomerId = command.CustomerId,
            Status = command.Status,
            TotalAmount = ParseOptionalDecimal(command.TotalAmount),
            TotalPrice = ParseOptionalDecimal(command.TotalPrice),
            ClearTotalPrice = command.ClearTotalPrice
        }, cancellationToken)).ToDto();
    }
}