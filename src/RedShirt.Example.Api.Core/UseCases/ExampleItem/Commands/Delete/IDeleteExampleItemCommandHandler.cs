namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Delete;

public interface IDeleteExampleItemCommandHandler
{
    Task Handle(DeleteExampleItemCommand command, CancellationToken cancellationToken = default);
}