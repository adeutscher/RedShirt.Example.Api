namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.IntegrationTests.Support;

/// <summary>
///     Mimics Kestrel <c>Request.Body</c>: async reads only, non-seekable, no synchronous I/O.
/// </summary>
internal sealed class AsyncOnlyReadStream : Stream
{
    private const string SyncReadNotSupportedMessage =
        "Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true.";

    private readonly int _chunkSize;

    private readonly byte[] _payload;
    private int _position;

    public int SyncReadAttempts { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public AsyncOnlyReadStream(byte[] payload, int chunkSize = 4)
    {
        _payload = payload;
        _chunkSize = Math.Max(1, chunkSize);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        SyncReadAttempts++;
        throw new InvalidOperationException(SyncReadNotSupportedMessage);
    }

    public override int Read(Span<byte> buffer)
    {
        SyncReadAttempts++;
        throw new InvalidOperationException(SyncReadNotSupportedMessage);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (_position >= _payload.Length)
        {
            return 0;
        }

        await Task.Yield();

        var toCopy = Math.Min(_chunkSize, _payload.Length - _position);
        _payload.AsSpan(_position, toCopy).CopyTo(buffer.Span);
        _position += toCopy;
        return toCopy;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}