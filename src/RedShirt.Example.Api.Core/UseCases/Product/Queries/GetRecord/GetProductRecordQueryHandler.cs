using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.GetRecord;

public interface IGetProductRecordQueryHandler : ICqrsHandler<GetProductRecordQuery, ProductModel>;

internal class GetProductRecordQueryHandler(
    IProductRepository repository,
    ICoreRequestValidator coreRequestValidator)
    : IGetProductRecordQueryHandler
{
    public async Task<ProductModel> Handle(GetProductRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);

        if (await repository.GetByIdAsync(query.Id, cancellationToken) is not { } entry)
        {
            throw new ResourceNotFoundException();
        }

        return entry;
    }
}
