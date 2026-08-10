namespace RedShirt.Example.Api.Core.UseCases.Foo.Commands.Create;

public record CreateFooCommand(string Name, string IdempotencyKey);