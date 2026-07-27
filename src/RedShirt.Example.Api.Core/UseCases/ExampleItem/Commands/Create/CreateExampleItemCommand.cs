using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;

public record CreateExampleItemCommand(ExampleItemModel Model, string IdempotencyKey);