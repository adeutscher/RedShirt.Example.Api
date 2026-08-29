using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;

public interface IUpdateProductCommandHandler : ICqrsHandler<UpdateProductCommand, ProductDto>;

internal class UpdateProductCommandHandler(
    IProductService productService,
    ICoreRequestValidator coreRequestValidator)
    : IUpdateProductCommandHandler
{
    public async Task<ProductDto> Handle(UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        return (await productService.PutAsync(new ProductServicePutRequest
        {
            Id = command.Id,
            Sku = command.Sku,
            Name = command.Name,
            Price = decimal.Parse(command.Price, CultureInfo.InvariantCulture)
        }, cancellationToken)).ToDto();
    }
}