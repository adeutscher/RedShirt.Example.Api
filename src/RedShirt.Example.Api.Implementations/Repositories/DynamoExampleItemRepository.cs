using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Core.Models;
using RedShirt.Example.Api.Core.Repositories;
using RedShirt.Example.Api.Implementations.Domain;
using System.Text.Json;
using ResourceNotFoundException = RedShirt.Example.Api.Core.Exceptions.ResourceNotFoundException;

namespace RedShirt.Example.Api.Implementations.Repositories;

internal class DynamoExampleItemRepository(
    IAmazonDynamoDB dynamoDbClient,
    IDynamoDBContext dynamoDbContext,
    IOptions<DynamoExampleItemRepository.ConfigurationModel> options) : IExampleItemRepository
{
    private DynamoDBOperationConfig GetOperationConfig()
    {
        return new DynamoDBOperationConfig
        {
            OverrideTableName = options.Value.TableName
        };
    }

    public async Task DeleteByName(string name, CancellationToken cancellationToken = default)
    {
        var resource = await GetByName(name, cancellationToken);
        await dynamoDbContext.DeleteAsync(resource, GetOperationConfig(), cancellationToken);
    }

    public async Task<ExampleItemModel> GetByName(string name, CancellationToken cancellationToken = default)
    {
        var obj = await dynamoDbContext.LoadAsync<ExampleItemMapping>(name, GetOperationConfig(), cancellationToken);

        if (obj is null)
        {
            throw new ResourceNotFoundException();
        }

        return new ExampleItemModel
        {
            Name = obj.Name
        };
    }

    public Task Put(ExampleItemModel model, CancellationToken cancellationToken = default)
    {
        return dynamoDbContext.SaveAsync(new ExampleItemMapping
        {
            Name = model.Name
        }, GetOperationConfig(), cancellationToken);
    }

    public async Task<ExampleItemListModel> GetListAsync(string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var scanRequest = new ScanRequest
        {
            TableName = options.Value.TableName
        };

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            scanRequest.ExclusiveStartKey =
                JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(
                    Convert.FromBase64String(continuationToken));
        }

        var response = await dynamoDbClient
            .ScanAsync(scanRequest, cancellationToken);

        var data = response.Items
            .Select(Document.FromAttributeMap)
            .Select(dynamoDbContext.FromDocument<ExampleItemModel>)
            .ToList();

        return new ExampleItemListModel
        {
            ContinuationToken = response.LastEvaluatedKey.Count == 0
                ? null
                : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(response.LastEvaluatedKey)),
            Items = data
        };
    }

    internal class ConfigurationModel
    {
        public required string TableName { get; init; }
    }
}