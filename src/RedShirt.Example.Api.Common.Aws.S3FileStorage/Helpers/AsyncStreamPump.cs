using System.IO.Pipelines;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;

/// <summary>
///     Copies a source stream into a <see cref="PipeWriter" /> using only asynchronous reads.
///     Used to bridge Kestrel's async-only <c>Request.Body</c> to the AWS SDK, which reads upload
///     streams synchronously regardless of whether <c>PutObjectAsync</c> or
///     <c>TransferUtility.UploadAsync</c> is called.
/// </summary>
internal static class AsyncStreamPump
{
    public static async Task PumpAsync(Stream source, PipeWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var memory = writer.GetMemory();
                var bytesRead = await source.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
                var flushResult = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCompleted || flushResult.IsCanceled)
                {
                    break;
                }
            }

            await writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex).ConfigureAwait(false);
            throw;
        }
    }
}
