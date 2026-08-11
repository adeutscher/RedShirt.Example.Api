namespace RedShirt.Example.Api.Core.UseCases.Bar.Commands.Create;

public record CreateBarCommand(string Name, string IdempotencyKey);