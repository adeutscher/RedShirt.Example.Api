using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;

public interface IPatchProductCommandHandler : ICqrsHandler<PatchProductCommand, ProductModel>;

internal class PatchProductCommandHandler(
    IProductRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IPatchProductCommandHandler
{
    public async Task<ProductModel> Handle(PatchProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        if (!ProductModelExtensions.AreChangesRequested(command.Sku, command.Name, command.Price))
        {
            throw new NoChangesToModifyException();
        }

        if (await repository.GetByIdAsync(command.Id, cancellationToken) is not { } existing)
        {
            throw new ResourceNotFoundException();
        }

        var candidate = new ProductModel
        {
            Id = command.Id,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Sku = command.Sku ?? existing.Sku,
            Name = command.Name ?? existing.Name,
            Price = command.Price ?? existing.Price
        };

        if (candidate.IsTheSameAs(existing))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(candidate, cancellationToken);
    }
}
