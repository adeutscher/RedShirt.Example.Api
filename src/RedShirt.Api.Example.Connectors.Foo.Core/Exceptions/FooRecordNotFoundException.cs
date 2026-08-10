namespace RedShirt.Api.Example.Connectors.Foo.Core.Exceptions;

/// <summary>
///     The Foo dependency reported that no record exists for the requested id (HTTP 404).
/// </summary>
public sealed class FooRecordNotFoundException(int id) : Exception($"Foo record {id} was not found.")
{
    public int Id => id;
}