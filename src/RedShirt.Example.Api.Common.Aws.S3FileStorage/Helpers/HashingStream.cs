using System.Security.Cryptography;

namespace RedShirt.Example.Api.Common.Aws.S3FileStorage.Helpers;

/// <summary>
///     Wraps a stream and computes a SHA-256 digest as bytes are read.
/// </summary>
internal sealed class HashingStream(Stream inner) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public string GetSha256Hex() => Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        if (read > 0)
        {
            _hash.AppendData(buffer, offset, read);
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            _hash.AppendData(buffer.Span[..read]);
        }

        return read;
    }
}
