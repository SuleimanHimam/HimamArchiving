using System.Security.Cryptography;

namespace Archiving.Infrastructure.Security.Crypto;

/// <summary>
/// Read-only stream that verifies and decrypts an AENC3 container frame by frame. Nothing is handed to
/// the caller until its GCM tag has verified, so a tampered object yields an exception rather than
/// corrupt bytes — the archive fails closed.
///
/// <para><b>Recognising the last frame.</b> The writer guarantees every non-final frame is exactly
/// <c>chunkSize + tag</c> bytes and the final one is strictly shorter. So a short read <i>is</i> the end
/// marker, and it is authenticated: a truncated object presents a frame that was sealed with
/// <c>isFinal = 0</c> but is now verified with <c>isFinal = 1</c>, and the tag check fails.</para>
///
/// <para>Forward-only. Frames are fixed-size, so byte-range seeking is a small extension (seek to
/// <c>header + (offset / chunkSize) * (chunkSize + tag)</c>) — deliberately not implemented here,
/// because HTTP range serving over an encrypted archive needs its own access-log semantics.</para>
/// </summary>
public sealed class AeadReadingStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _leaveOpen;
    private readonly byte[] _header;
    private readonly byte[] _dek;
    private readonly byte[] _noncePrefix;
    private readonly byte[] _frame;      // ciphertext + tag as read from disk
    private readonly byte[] _plain;      // verified plaintext awaiting the caller
    private readonly int _chunkSize;

    private AesGcm? _gcm;
    private int _plainLen;
    private int _plainPos;
    private uint _frameIndex;
    private bool _eof;
    private long _position;

    private AeadReadingStream(Stream inner, byte[] header, ReadOnlySpan<byte> dek, bool leaveOpen)
    {
        _inner = inner;
        _leaveOpen = leaveOpen;
        _header = header;
        (_chunkSize, _noncePrefix) = AeadFormat.ReadHeader(header);
        _dek = dek.ToArray();
        _gcm = new AesGcm(_dek, AeadFormat.TagLen);
        _frame = new byte[_chunkSize + AeadFormat.TagLen];
        _plain = new byte[_chunkSize];
    }

    /// <summary>Reads and validates the container header, then returns a stream positioned at the first
    /// frame. Throws <see cref="CryptographicFormatException"/> if <paramref name="inner"/> is not an
    /// AENC3 container — check with <see cref="AeadFormat.IsAenc3"/> first if you need to fall back.</summary>
    public static async Task<AeadReadingStream> OpenAsync(Stream inner, ReadOnlyMemory<byte> dek, bool leaveOpen = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (dek.Length != AeadFormat.KeyLen) throw new ArgumentException("DEK must be 32 bytes (AES-256).", nameof(dek));

        var header = new byte[AeadFormat.HeaderLen];
        await inner.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        return new AeadReadingStream(inner, header, dek.Span, leaveOpen);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_gcm is null, this);
        if (destination.IsEmpty) return 0;

        if (_plainPos == _plainLen)
        {
            if (_eof) return 0;
            if (!await NextFrameAsync(ct).ConfigureAwait(false)) return 0;
        }

        var take = Math.Min(destination.Length, _plainLen - _plainPos);
        _plain.AsMemory(_plainPos, take).CopyTo(destination);
        _plainPos += take;
        _position += take;
        return take;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    /// <summary>Reads the next frame and verifies it. Returns false at end of container.</summary>
    private async ValueTask<bool> NextFrameAsync(CancellationToken ct)
    {
        var read = await ReadAtMostAsync(_frame, ct).ConfigureAwait(false);

        // A well-formed container always ends with a short final frame, so a clean zero-byte read here
        // means the trailing frame was removed wholesale.
        if (read == 0) throw new CryptographicFormatException("Container ends without a final frame — truncated.");
        if (read < AeadFormat.TagLen) throw new CryptographicFormatException("Trailing bytes are too short to be a frame.");

        var isFinal = read < _frame.Length;
        var payloadLen = read - AeadFormat.TagLen;

        Span<byte> nonce = stackalloc byte[AeadFormat.NonceLen];
        Span<byte> aad = stackalloc byte[AeadFormat.AadLen];
        AeadFormat.BuildNonce(nonce, _noncePrefix, _frameIndex);
        AeadFormat.BuildAad(aad, _header, _frameIndex, isFinal);

        // Throws AuthenticationTagMismatchException on any tampering, reordering or truncation.
        _gcm!.Decrypt(nonce, _frame.AsSpan(0, payloadLen), _frame.AsSpan(payloadLen, AeadFormat.TagLen),
                      _plain.AsSpan(0, payloadLen), aad);

        _plainLen = payloadLen;
        _plainPos = 0;
        _frameIndex++;
        _eof = isFinal;
        return payloadLen > 0 || !isFinal;
    }

    /// <summary>Fills <paramref name="buffer"/> as far as the stream allows — a short read means EOF,
    /// not a partial network read, which is what the final-frame rule depends on.</summary>
    private async ValueTask<int> ReadAtMostAsync(byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await _inner.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (_gcm is not null)
        {
            Wipe();
            if (!_leaveOpen) await _inner.DisposeAsync().ConfigureAwait(false);
        }
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _gcm is not null)
        {
            Wipe();
            if (!_leaveOpen) _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Wipe()
    {
        _gcm?.Dispose();
        _gcm = null;
        CryptographicOperations.ZeroMemory(_dek);
        CryptographicOperations.ZeroMemory(_plain);   // decrypted document bytes must not outlive the request
        CryptographicOperations.ZeroMemory(_frame);
    }
}
