using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;

public interface IExampleItemRepository
{
    Task DeleteByName(string name, CancellationToken cancellationToken = default);
    Task<ExampleItemModel> GetByName(string name, CancellationToken cancellationToken = default);
    Task<ExampleItemListModel> GetListAsync(string? continuationToken, CancellationToken cancellationToken = default);
    Task Put(ExampleItemModel model, CancellationToken cancellationToken = default);
}