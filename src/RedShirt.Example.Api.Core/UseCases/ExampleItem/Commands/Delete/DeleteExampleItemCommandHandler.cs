using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Delete;

public interface IDeleteExampleItemCommandHandler : ICqrsHandler<DeleteExampleItemCommand>;

internal class DeleteExampleItemCommandHandler(
    IExampleItemRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IDeleteExampleItemCommandHandler
{
    public async Task Handle(DeleteExampleItemCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await repository.DeleteByName(command.Name, cancellationToken);
    }
}
