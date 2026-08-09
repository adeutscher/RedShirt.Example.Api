using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;

public interface IUpdateProductCommandHandler : ICqrsHandler<UpdateProductCommand, ProductModel>;

internal class UpdateProductCommandHandler(
    IProductRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IUpdateProductCommandHandler
{
    public async Task<ProductModel> Handle(UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        var existing = await repository.GetByIdAsync(command.Id, cancellationToken);
        var createdAt = DateTime.UtcNow;
        var model = new ProductModel
        {
            Id = command.Id,
            CreatedAtUtc = existing?.CreatedAtUtc ?? createdAt,
            UpdatedAtUtc = createdAt,
            Sku = command.Sku,
            Name = command.Name,
            Price = command.Price
        };

        if (existing is not null && existing.IsTheSameAs(model))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(model, cancellationToken);
    }
}
