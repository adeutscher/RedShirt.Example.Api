namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;

public record UpdateProductCommand(Guid Id, string Sku, string Name, string Price);