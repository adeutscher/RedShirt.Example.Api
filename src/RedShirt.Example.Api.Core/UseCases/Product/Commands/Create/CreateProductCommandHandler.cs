using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Create;

public interface ICreateProductCommandHandler : ICqrsHandler<CreateProductCommand, ProductModel>;

internal class CreateProductCommandHandler(
    IProductRepository repository,
    ICacheBasedIdempotencyWrapperService idempotencyWrapperService,
    ICoreRequestValidator coreRequestValidator)
    : ICreateProductCommandHandler
{
    public async Task<ProductModel> Handle(CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return await idempotencyWrapperService.RunIdempotentlyAsync(command.IdempotencyKey, async () =>
        {
            var createdAt = DateTime.UtcNow;
            var model = new ProductModel
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt,
                Sku = command.Sku,
                Name = command.Name,
                Price = command.Price
            };

            return await repository.UpsertAsync(model, cancellationToken);
        }, cancellationToken);
    }
}
