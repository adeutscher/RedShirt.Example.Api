using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetInternalDetails;

public sealed record GetUploadInternalDetailsQuery(Guid Id);

public interface IGetUploadInternalDetailsQueryHandler
    : ICqrsHandler<GetUploadInternalDetailsQuery, UploadInternalDetailsModel>;

internal sealed class GetUploadInternalDetailsQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IGetUploadInternalDetailsQueryHandler
{
    public async Task<UploadInternalDetailsModel> Handle(GetUploadInternalDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        var details = await uploadService.GetDetailsAsync(query.Id, cancellationToken);
        return details.ToInternalDetailsModel();
    }
}