using Amazon.DynamoDBv2.DataModel;

namespace RedShirt.Example.Api.Implementations.Domain;

public class ExampleItemMapping
{
    [DynamoDBHashKey]
    public required string Name { get; set; }
}