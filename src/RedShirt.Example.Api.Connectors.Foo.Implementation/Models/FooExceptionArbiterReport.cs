namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Models;

internal sealed class FooExceptionArbiterReport
{
    public required bool AlreadyHandled { get; init; }
    public required bool IsExpected { get; init; }
    public required bool CouldBeTransient { get; init; }
    public required bool CouldBeExternallySolvable { get; init; }
}