namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;

public record PatchProductCommand(Guid Id, string? Sku, string? Name, string? Price);
