using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;

public sealed record CreateUploadCommand(
    string FileName,
    string UploadedByUserId,
    string UploadedByUsername,
    string UploaderIpAddress,
    Stream Content,
    long? ContentLength,
    string IdempotencyKey);

public interface ICreateUploadCommandHandler : ICqrsHandler<CreateUploadCommand, UploadSummaryModel>;

internal sealed class CreateUploadCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ICreateUploadCommandHandler
{
    public async Task<UploadSummaryModel> Handle(CreateUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await uploadService.CreateAsync(new UploadServiceCreateRequest
        {
            FileName = command.FileName,
            UploadedByUserId = command.UploadedByUserId,
            UploadedByUsername = command.UploadedByUsername,
            UploaderIpAddress = command.UploaderIpAddress,
            Content = command.Content,
            ContentLength = command.ContentLength,
            IdempotencyKey = command.IdempotencyKey
        }, cancellationToken);
    }
}