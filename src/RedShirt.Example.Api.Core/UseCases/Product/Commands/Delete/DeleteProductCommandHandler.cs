using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Delete;

public interface IDeleteProductCommandHandler : ICqrsHandler<DeleteProductCommand>;

internal class DeleteProductCommandHandler(
    IProductRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IDeleteProductCommandHandler
{
    public async Task Handle(DeleteProductCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        if (!await repository.DeleteAsync(command.Id, cancellationToken))
        {
            throw new ResourceNotFoundException();
        }
    }
}
