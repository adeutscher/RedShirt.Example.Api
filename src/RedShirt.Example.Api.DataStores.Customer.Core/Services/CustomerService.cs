using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Repositories;

namespace RedShirt.Example.Api.DataStores.Customer.Core.Services;

public sealed class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw new ResourceNotFoundException();
        }
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await repository.GetByIdAsync(id, cancellationToken) is not { } entry)
        {
            throw new ResourceNotFoundException();
        }

        return entry;
    }

    public async Task<CustomerDto> PatchAsync(CustomerServicePatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.AreChangesRequested())
        {
            throw new NoChangesToModifyException();
        }

        if (await repository.GetByIdAsync(request.Id, cancellationToken) is not { } existing)
        {
            throw new ResourceNotFoundException();
        }

        var candidate = new CustomerDto
        {
            Id = request.Id,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Email = request.Email ?? existing.Email,
            DisplayName = request.DisplayName ?? existing.DisplayName
        };

        if (candidate.IsTheSameAs(existing))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(candidate, cancellationToken);
    }

    public async Task<CustomerDto> PostAsync(CustomerServicePostRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new BadRequestException("DisplayName cannot be empty.");
        }

        var createdAt = DateTime.UtcNow;
        var dto = new CustomerDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        return await repository.UpsertAsync(dto, cancellationToken);
    }

    public async Task<CustomerDto> PutAsync(CustomerServicePutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new BadRequestException("DisplayName cannot be empty.");
        }

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        var createdAt = DateTime.UtcNow;
        var dto = new CustomerDto
        {
            Id = request.Id,
            CreatedAtUtc = existing?.CreatedAtUtc ?? createdAt,
            UpdatedAtUtc = createdAt,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        if (existing is not null && existing.IsTheSameAs(dto))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(dto, cancellationToken);
    }

    public Task<CustomerSearchResponse> SearchAsync(CustomerServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return repository.SearchAsync(parameters, continuationToken, cancellationToken);
    }
}
