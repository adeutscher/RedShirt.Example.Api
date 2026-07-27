using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;

public interface ICreateExampleItemCommandHandler
{
    Task<ExampleItemModel> Handle(CreateExampleItemCommand command, CancellationToken cancellationToken = default);
}