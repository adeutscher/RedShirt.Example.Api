using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.GetRecord;

public interface IGetExampleItemRecordQueryHandler
{
    Task<ExampleItemModel> Handle(GetExampleItemRecordQuery query, CancellationToken cancellationToken = default);
}