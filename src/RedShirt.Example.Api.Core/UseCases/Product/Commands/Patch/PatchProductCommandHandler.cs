using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;

public interface IPatchProductCommandHandler : ICqrsHandler<PatchProductCommand, ProductDto>;

internal class PatchProductCommandHandler(
    IProductService productService,
    ICoreRequestValidator coreRequestValidator)
    : IPatchProductCommandHandler
{
    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task<ProductDto> Handle(PatchProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return (await productService.PatchAsync(new ProductServicePatchRequest
        {
            Id = command.Id,
            Sku = command.Sku,
            Name = command.Name,
            Price = ParseOptionalDecimal(command.Price)
        }, cancellationToken)).ToDto();
    }
}