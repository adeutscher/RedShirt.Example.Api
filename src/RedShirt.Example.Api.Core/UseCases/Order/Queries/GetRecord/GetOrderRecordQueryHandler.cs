using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.GetRecord;

public interface IGetOrderRecordQueryHandler : ICqrsHandler<GetOrderRecordQuery, OrderDto>;

internal class GetOrderRecordQueryHandler(
    IOrderService orderService,
    ICoreRequestValidator coreRequestValidator)
    : IGetOrderRecordQueryHandler
{
    public async Task<OrderDto> Handle(GetOrderRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await orderService.GetByIdAsync(query.Id, cancellationToken);
    }
}
