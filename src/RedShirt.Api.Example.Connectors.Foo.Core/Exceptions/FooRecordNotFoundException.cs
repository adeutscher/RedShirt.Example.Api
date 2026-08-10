namespace RedShirt.Api.Example.Connectors.Foo.Core.Exceptions;

/// <summary>
///     The Foo dependency reported that no record exists for the requested id (HTTP 404).
/// </summary>
public sealed class FooRecordNotFoundException : Exception
{
    public int Id { get; }

    public FooRecordNotFoundException(int id)
        : base($"Foo record {id} was not found.")
    {
        Id = id;
    }

    public FooRecordNotFoundException(int id, Exception innerException)
        : base($"Foo record {id} was not found.", innerException)
    {
        Id = id;
    }
}