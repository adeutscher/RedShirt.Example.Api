using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Delete;

public interface IDeleteProductCommandHandler : ICqrsHandler<DeleteProductCommand>;

internal class DeleteProductCommandHandler(
    IProductService productService,
    ICoreRequestValidator coreRequestValidator)
    : IDeleteProductCommandHandler
{
    public async Task Handle(DeleteProductCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await productService.DeleteAsync(command.Id, cancellationToken);
    }
}
