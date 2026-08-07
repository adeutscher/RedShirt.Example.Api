namespace RedShirt.Example.Api.Common.Aws.Models;

public sealed class AwsExceptionArbiterReport
{
    public required bool IsExpected { get; init; }
    public required bool CouldBeTransient { get; init; }
    public required bool CouldBeExternallySolvable { get; init; }
}