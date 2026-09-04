namespace RedShirt.Example.Api.DataStores.Common.DapperMySql.Models;

internal sealed class MySqlExceptionArbiterReport
{
    public required bool AlreadyHandled { get; init; }
    public required bool IsExpected { get; init; }
    public required bool CouldBeTransient { get; init; }
    public required bool CouldBeExternallySolvable { get; init; }
}