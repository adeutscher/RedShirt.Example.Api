using RedShirt.Api.Example.Connectors.Foo.Core.Models;
using RedShirt.Api.Example.Connectors.Foo.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Foo.Queries.GetRecord;

public interface IGetFooRecordQueryHandler : ICqrsHandler<GetFooRecordQuery, GetFooConnectorResponse>;

internal class GetFooRecordQueryHandler(
    IFooConnector fooConnector,
    ICoreRequestValidator coreRequestValidator)
    : IGetFooRecordQueryHandler
{
    public async Task<GetFooConnectorResponse> Handle(GetFooRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await fooConnector.GetByIdAsync(query.Id, cancellationToken);
    }
}
