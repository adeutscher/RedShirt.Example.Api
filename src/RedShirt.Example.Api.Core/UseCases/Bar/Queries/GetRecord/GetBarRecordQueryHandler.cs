using RedShirt.Example.Api.Connectors.Bar.Core.Models;
using RedShirt.Example.Api.Connectors.Bar.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Bar.Queries.GetRecord;

public interface IGetBarRecordQueryHandler : ICqrsHandler<GetBarRecordQuery, GetBarConnectorResponse>;

internal class GetBarRecordQueryHandler(
    IBarConnector barConnector,
    ICoreRequestValidator coreRequestValidator)
    : IGetBarRecordQueryHandler
{
    public async Task<GetBarConnectorResponse> Handle(GetBarRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await barConnector.GetByIdAsync(query.Id, cancellationToken);
    }
}