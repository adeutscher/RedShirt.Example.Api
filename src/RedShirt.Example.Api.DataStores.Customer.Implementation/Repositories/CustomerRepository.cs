using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.DataStores.Constants;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Entities;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Factories;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Predicates;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Repositories;

internal sealed class CustomerRepository(
    ICustomerDbContextFactory dbContextFactory,
    IRemoteCacheService cacheService) : ICustomerRepository
{
    private const int MaxPageSize = 100;
    private const string ConnectionStringName = DatabaseConstants.PrimaryDatabaseConnectionStringName;

    private static PredicateBuilder<CustomerEntity> BuildSearchPredicate(CustomerServiceSearchRequest parameters)
    {
        var builder = new PredicateBuilder<CustomerEntity>();

        if (parameters.Id.HasValue)
        {
            var id = parameters.Id.Value;
            builder.And(c => c.Id == id);
        }

        if (parameters.CreatedBeforeUtc.HasValue)
        {
            var createdBefore = parameters.CreatedBeforeUtc.Value;
            builder.And(c => c.CreatedAtUtc < createdBefore);
        }

        if (parameters.CreatedAfterUtc.HasValue)
        {
            var createdAfter = parameters.CreatedAfterUtc.Value;
            builder.And(c => c.CreatedAtUtc > createdAfter);
        }

        if (parameters.UpdatedBeforeUtc.HasValue)
        {
            var updatedBefore = parameters.UpdatedBeforeUtc.Value;
            builder.And(c => c.UpdatedAtUtc < updatedBefore);
        }

        if (parameters.UpdatedAfterUtc.HasValue)
        {
            var updatedAfter = parameters.UpdatedAfterUtc.Value;
            builder.And(c => c.UpdatedAtUtc > updatedAfter);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Email))
        {
            var email = parameters.Email;
            builder.And(c => c.Email == email);
        }

        if (!string.IsNullOrWhiteSpace(parameters.EmailContains))
        {
            var emailContains = parameters.EmailContains;
            builder.And(c => c.Email.Contains(emailContains));
        }

        if (!string.IsNullOrWhiteSpace(parameters.DisplayName))
        {
            var displayName = parameters.DisplayName;
            builder.And(c => c.DisplayName == displayName);
        }

        // ReSharper disable once InvertIf
        if (!string.IsNullOrWhiteSpace(parameters.DisplayNameContains))
        {
            var displayNameContains = parameters.DisplayNameContains;
            builder.And(c => c.DisplayName.Contains(displayNameContains));
        }

        return builder;
    }

    private static CustomerDto ToDto(CustomerEntity entity)
    {
        return new CustomerDto
        {
            Id = entity.Id,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Email = entity.Email,
            DisplayName = entity.DisplayName
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        var entity = await context.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        context.Customers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        var entity = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<CustomerDto> UpsertAsync(CustomerDto item, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        var existing = await context.Customers.FirstOrDefaultAsync(c => c.Id == item.Id, cancellationToken);
        if (existing is null)
        {
            context.Customers.Add(new CustomerEntity
            {
                Id = item.Id,
                CreatedAtUtc = item.CreatedAtUtc,
                UpdatedAtUtc = item.UpdatedAtUtc,
                Email = item.Email,
                DisplayName = item.DisplayName
            });
        }
        else
        {
            existing.Email = item.Email;
            existing.DisplayName = item.DisplayName;
            existing.UpdatedAtUtc = item.UpdatedAtUtc;
            // Preserve original CreatedAtUtc from the entity row.
        }

        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<CustomerSearchResponse> SearchAsync(CustomerServiceSearchRequest parameters,
        Guid? continuationToken, CancellationToken cancellationToken = default)
    {
        ContinuationParameters? continuationParameters = null;
        if (continuationToken is not null)
        {
            var continuationParametersKey = $"continuation:{continuationToken.Value}";
            continuationParameters =
                await cacheService.GetObjectAsync<ContinuationParameters>(continuationParametersKey, cancellationToken);
        }

        if (continuationParameters is not null)
        {
            parameters = continuationParameters.SearchParameters;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var predicate = BuildSearchPredicate(parameters);
        if (continuationParameters is not null)
        {
            var checkpoint = continuationParameters.LastUpdatedAtUtc;
            var lastId = continuationParameters.LastId;
            predicate.And(c => c.UpdatedAtUtc <= checkpoint && c.Id != lastId);
        }

        var pageSize = parameters.PageSize;
        if (pageSize <= 0)
        {
            pageSize = MaxPageSize;
        }

        pageSize = Math.Min(MaxPageSize, pageSize);

        var entities = await context.Customers.AsNoTracking()
            .Where(predicate.Build())
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ThenByDescending(c => c.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var records = entities.Select(ToDto).ToList();

        continuationToken = records.Count >= pageSize ? Guid.NewGuid() : null;

        // ReSharper disable once InvertIf
        if (continuationToken.HasValue)
        {
            var lastRecord = records[^1];
            await cacheService.SetObjectAsync(
                $"continuation:{continuationToken.Value}",
                new ContinuationParameters
                {
                    SearchParameters = parameters,
                    LastId = lastRecord.Id,
                    LastUpdatedAtUtc = lastRecord.UpdatedAtUtc
                },
                TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        return new CustomerSearchResponse
        {
            Records = records,
            ContinuationToken = continuationToken
        };
    }

    private sealed class ContinuationParameters
    {
        public required CustomerServiceSearchRequest SearchParameters { get; init; }
        public required Guid LastId { get; init; }
        public required DateTime LastUpdatedAtUtc { get; init; }
    }
}