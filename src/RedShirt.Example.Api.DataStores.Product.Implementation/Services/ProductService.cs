using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;
using RedShirt.Example.Api.DataStores.Product.Implementation.Repositories;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Services;

internal sealed class ProductService(IProductRepository repository) : IProductService
{
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw new ResourceNotFoundException();
        }
    }

    public async Task<ProductInternalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await repository.GetByIdAsync(id, cancellationToken) is not { } entry)
        {
            throw new ResourceNotFoundException();
        }

        return entry;
    }

    public async Task<ProductInternalDto> PatchAsync(ProductServicePatchRequest request,
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

        var candidate = new ProductInternalDto
        {
            Id = request.Id,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Sku = request.Sku ?? existing.Sku,
            Name = request.Name ?? existing.Name,
            Price = request.Price ?? existing.Price
        };

        if (candidate.IsTheSameAs(existing))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(candidate, cancellationToken);
    }

    public async Task<ProductInternalDto> PostAsync(ProductServicePostRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new BadRequestException("Sku cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Name cannot be empty.");
        }

        var createdAt = DateTime.UtcNow;
        var dto = new ProductInternalDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            Sku = request.Sku,
            Name = request.Name,
            Price = request.Price
        };

        return await repository.UpsertAsync(dto, cancellationToken);
    }

    public async Task<ProductInternalDto> PutAsync(ProductServicePutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new BadRequestException("Sku cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Name cannot be empty.");
        }

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        var createdAt = DateTime.UtcNow;
        var dto = new ProductInternalDto
        {
            Id = request.Id,
            CreatedAtUtc = existing?.CreatedAtUtc ?? createdAt,
            UpdatedAtUtc = createdAt,
            Sku = request.Sku,
            Name = request.Name,
            Price = request.Price
        };

        if (existing is not null && existing.IsTheSameAs(dto))
        {
            throw new NoChangesToModifyException();
        }

        return await repository.UpsertAsync(dto, cancellationToken);
    }

    public Task<ProductServiceSearchResponse> SearchAsync(ProductServiceSearchRequest parameters,
        Guid? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return repository.SearchAsync(parameters, continuationToken,
            cancellationToken);
    }
}