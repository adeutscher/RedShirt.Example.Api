namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Create;

public record CreateProductCommand(string Sku, string Name, string Price, string IdempotencyKey);
