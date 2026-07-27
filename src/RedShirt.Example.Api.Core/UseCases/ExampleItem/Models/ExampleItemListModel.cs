namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

public class ExampleItemListModel
{
    public required string? ContinuationToken { get; set; }
    public required List<ExampleItemModel> Items { get; set; }
}