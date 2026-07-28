using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.ListRecords;

public interface IListExampleItemRecordsQueryHandler : ICqrsHandler<ListExampleItemRecordsQuery, ExampleItemListModel>;

internal class ListExampleItemRecordsQueryHandler(IExampleItemRepository repository)
    : IListExampleItemRecordsQueryHandler
{
    public Task<ExampleItemListModel> Handle(ListExampleItemRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        return repository.GetListAsync(query.ContinuationToken, cancellationToken);
    }
}
