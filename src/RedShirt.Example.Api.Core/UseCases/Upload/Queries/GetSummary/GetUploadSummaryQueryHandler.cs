using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;

public sealed record GetUploadSummaryQuery(Guid Id);

public interface IGetUploadSummaryQueryHandler : ICqrsHandler<GetUploadSummaryQuery, UploadSummaryModel>;

internal sealed class GetUploadSummaryQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IGetUploadSummaryQueryHandler
{
    public async Task<UploadSummaryModel> Handle(GetUploadSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.GetSummaryAsync(query.Id, cancellationToken);
    }
}