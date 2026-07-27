using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.GetRecord;

internal class GetExampleItemRecordQueryHandler(
    IExampleItemRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IGetExampleItemRecordQueryHandler
{
    public async Task<ExampleItemModel> Handle(GetExampleItemRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await repository.GetByName(query.Name, cancellationToken);
    }
}