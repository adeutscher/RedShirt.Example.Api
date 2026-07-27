using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.ListRecords;

public interface IListExampleItemRecordsQueryHandler
{
    Task<ExampleItemListModel> Handle(ListExampleItemRecordsQuery query,
        CancellationToken cancellationToken = default);
}