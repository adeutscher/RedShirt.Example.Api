using RedShirt.Example.Api.Connectors.Foo.Core.Models;
using RedShirt.Example.Api.Connectors.Foo.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Foo.Commands.Create;

public interface ICreateFooCommandHandler : ICqrsHandler<CreateFooCommand, CreateFooConnectorResponse>;

internal class CreateFooCommandHandler(
    IFooConnector fooConnector,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateFooCommandHandler
{
    public async Task<CreateFooConnectorResponse> Handle(CreateFooCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
            await fooConnector.CreateAsync(new CreateFooConnectorRequest
            {
                Name = command.Name
            }, cancellationToken), cancellationToken);
    }
}