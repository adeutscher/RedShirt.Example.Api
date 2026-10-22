using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Create;

public interface ICreateProductCommandHandler : ICqrsHandler<CreateProductCommand, ProductDto>;

internal class CreateProductCommandHandler(
    IProductService productService,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateProductCommandHandler
{
    public async Task<ProductDto> Handle(CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
            await productService.PostAsync(new ProductServicePostRequest
            {
                Sku = command.Sku,
                Name = command.Name,
                Price = command.Price
            }, cancellationToken), cancellationToken);
    }
}
