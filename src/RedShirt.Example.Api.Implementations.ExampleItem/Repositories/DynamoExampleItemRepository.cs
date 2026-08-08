using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Services;
using RedShirt.Example.Api.Implementations.ExampleItem.Domain;
using System.Text.Json;
using BadRequestException = RedShirt.Example.Api.Common.Exceptions.Responses.BadRequestException;
using ResourceNotFoundException = RedShirt.Example.Api.Common.Exceptions.Responses.ResourceNotFoundException;

namespace RedShirt.Example.Api.Implementations.ExampleItem.Repositories;

internal class DynamoExampleItemRepository(
    IAmazonDynamoDB dynamoDbClient,
    IDynamoDBContext dynamoDbContext,
    IOptions<DynamoExampleItemRepository.ConfigurationModel> options) : IExampleItemRepository
{
    public async Task DeleteByName(string name, CancellationToken cancellationToken = default)
    {
        var resource = await GetByName(name, cancellationToken);
        await dynamoDbContext.DeleteAsync(resource, new DeleteConfig {OverrideTableName = options.Value.TableName},
            cancellationToken);
    }

    public async Task<ExampleItemModel> GetByName(string name, CancellationToken cancellationToken = default)
    {
        var obj = await dynamoDbContext.LoadAsync<ExampleItemMapping>(name,
            new LoadConfig {OverrideTableName = options.Value.TableName}, cancellationToken);

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
        }, new SaveConfig {OverrideTableName = options.Value.TableName}, cancellationToken);
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
            try
            {
                var tokenBytes = Convert.FromBase64String(continuationToken);

                scanRequest.ExclusiveStartKey =
                    JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(tokenBytes);
            }
            catch (FormatException)
            {
                throw new BadRequestException("Continuation token must be a valid base64-encoded string");
            }
            catch (JsonException)
            {
                throw new BadRequestException("Continuation token is not valid");
            }
        }

        var response = await dynamoDbClient
            .ScanAsync(scanRequest, cancellationToken);

        var data = response.Items
            .Select(Document.FromAttributeMap)
            .Select(dynamoDbContext.FromDocument<ExampleItemModel>)
            .ToList();

        return new ExampleItemListModel
        {
            ContinuationToken = (response.LastEvaluatedKey?.Count ?? 0) == 0
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