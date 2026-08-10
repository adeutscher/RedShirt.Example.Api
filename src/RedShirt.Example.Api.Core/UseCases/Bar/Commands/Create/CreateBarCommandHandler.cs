using RedShirt.Api.Example.Connectors.Bar.Core.Models;
using RedShirt.Api.Example.Connectors.Bar.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Bar.Commands.Create;

public interface ICreateBarCommandHandler : ICqrsHandler<CreateBarCommand, CreateBarConnectorResponse>;

internal class CreateBarCommandHandler(
    IBarConnector barConnector,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateBarCommandHandler
{
    public async Task<CreateBarConnectorResponse> Handle(CreateBarCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
            await barConnector.CreateAsync(new CreateBarConnectorRequest
            {
                Name = command.Name
            }, cancellationToken), cancellationToken);
    }
}