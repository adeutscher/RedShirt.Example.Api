using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;

public interface ICreateExampleItemCommandHandler : ICqrsHandler<CreateExampleItemCommand, ExampleItemModel>;

internal class CreateExampleItemCommandHandler(
    IExampleItemRepository repository,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateExampleItemCommandHandler
{
    public async Task<ExampleItemModel> Handle(CreateExampleItemCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
        {
            await repository.Put(command.Model, cancellationToken);
            return command.Model;
        }, cancellationToken);
    }
}