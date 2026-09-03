using System.Security.Cryptography;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;

/// <summary>
///     Wraps a stream, computes a SHA-256 digest as bytes are read, and ensures the upstream
///     stream is only ever read asynchronously even when consumers (e.g. TransferUtility) call
///     synchronous <see cref="Read" />.
/// </summary>
internal sealed class HashingStream(Stream inner) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public long BytesRead { get; private set; }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public string GetSha256Hex()
    {
        var hex = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        _hash.Dispose();
        return hex;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override int Read(Span<byte> buffer)
    {
        return Read(buffer.ToArray(), 0, buffer.Length);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            BytesRead += read;
            _hash.AppendData(buffer.Span[..read]);
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
    }
}