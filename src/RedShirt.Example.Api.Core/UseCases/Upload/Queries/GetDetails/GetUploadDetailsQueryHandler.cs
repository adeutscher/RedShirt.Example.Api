using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;

public sealed record GetUploadDetailsQuery(Guid Id);

public interface IGetUploadDetailsQueryHandler : ICqrsHandler<GetUploadDetailsQuery, UploadDetailsModel>;

internal sealed class GetUploadDetailsQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IGetUploadDetailsQueryHandler
{
    public async Task<UploadDetailsModel> Handle(GetUploadDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.GetDetailsAsync(query.Id, cancellationToken);
    }
}
