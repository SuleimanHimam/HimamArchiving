using System.Security.Cryptography;

namespace Archiving.Infrastructure.Security.Crypto;

/// <summary>
/// Write-only stream that seals plaintext into AENC3 frames (AES-256-GCM) as it is written, so a
/// document is encrypted <b>in flight</b> and the cleartext never reaches the file system.
///
/// <para><b>Final-frame rule.</b> A full buffer is always flushed as a non-final frame; whatever remains
/// at close is written as the final frame. The remainder is therefore always in <c>[0, chunkSize)</c>,
/// which means (a) the last frame on disk is always strictly shorter than a full frame — the reader uses
/// that to recognise it without a length field — and (b) an empty object still costs one 16-byte frame,
/// so "truncate the file to its header" is not a silent way to produce an empty document.</para>
///
/// <para>The stream owns nothing it did not create: <paramref name="dek"/> is copied and zeroed on
/// dispose, and the inner stream is left open unless <paramref name="leaveOpen"/> says otherwise.</para>
/// </summary>
public sealed class AeadWritingStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _leaveOpen;
    private readonly byte[] _header = new byte[AeadFormat.HeaderLen];
    private readonly byte[] _dek;
    private readonly byte[] _buffer;
    private readonly byte[] _frame;          // ciphertext staging (chunkSize) + tag appended separately
    private readonly byte[] _noncePrefix;
    private readonly int _chunkSize;

    private AesGcm? _gcm;
    private int _bufLen;
    private uint _frameIndex;
    private bool _headerWritten;
    private bool _finished;
    private long _plaintextLength;

    public AeadWritingStream(Stream inner, ReadOnlySpan<byte> dek, int chunkSize = AeadFormat.DefaultChunkSize, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (!inner.CanWrite) throw new ArgumentException("Inner stream is not writable.", nameof(inner));
        if (dek.Length != AeadFormat.KeyLen) throw new ArgumentException("DEK must be 32 bytes (AES-256).", nameof(dek));
        if (chunkSize is < AeadFormat.MinChunkSize or > AeadFormat.MaxChunkSize)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        _inner = inner;
        _leaveOpen = leaveOpen;
        _chunkSize = chunkSize;
        _dek = dek.ToArray();
        _gcm = new AesGcm(_dek, AeadFormat.TagLen);
        _buffer = new byte[chunkSize];
        _frame = new byte[chunkSize + AeadFormat.TagLen];
        _noncePrefix = RandomNumberGenerator.GetBytes(AeadFormat.NoncePrefixLen);

        AeadFormat.WriteHeader(_header, chunkSize, _noncePrefix);
    }

    /// <summary>Plaintext bytes accepted so far — what the metadata index should record as the size.</summary>
    public long PlaintextLength => _plaintextLength;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _plaintextLength; set => throw new NotSupportedException(); }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_gcm is null, this);
        if (_finished) throw new InvalidOperationException("The container has already been finalised.");

        await EnsureHeaderAsync(ct).ConfigureAwait(false);

        while (!source.IsEmpty)
        {
            var take = Math.Min(_chunkSize - _bufLen, source.Length);
            source[..take].CopyTo(_buffer.AsMemory(_bufLen));
            _bufLen += take;
            _plaintextLength += take;
            source = source[take..];

            // Only ever flushed when full — so the remainder at close is < chunkSize and the reader can
            // identify the final frame by its length alone.
            if (_bufLen == _chunkSize) await FlushFrameAsync(isFinal: false, ct).ConfigureAwait(false);
        }
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

    /// <summary>Seals the trailing frame and commits the container. Idempotent, and called automatically
    /// on dispose — but call it explicitly when you need the write to have failed loudly before you
    /// record the object in the metadata index.</summary>
    public async Task FinishAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_gcm is null, this);
        if (_finished) return;

        await EnsureHeaderAsync(ct).ConfigureAwait(false);
        await FlushFrameAsync(isFinal: true, ct).ConfigureAwait(false);
        _finished = true;
        await _inner.FlushAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask EnsureHeaderAsync(CancellationToken ct)
    {
        if (_headerWritten) return;
        await _inner.WriteAsync(_header, ct).ConfigureAwait(false);
        _headerWritten = true;
    }

    private async ValueTask FlushFrameAsync(bool isFinal, CancellationToken ct)
    {
        // Sealing is kept in its own synchronous method: the nonce and AAD are stack-allocated, and a
        // stack buffer cannot survive an await boundary.
        var frameLen = SealFrame(isFinal);
        await _inner.WriteAsync(_frame.AsMemory(0, frameLen), ct).ConfigureAwait(false);
        _bufLen = 0;
        _frameIndex++;
    }

    /// <summary>Seals the buffered plaintext into <c>_frame</c> and returns the frame length on disk.</summary>
    private int SealFrame(bool isFinal)
    {
        Span<byte> nonce = stackalloc byte[AeadFormat.NonceLen];
        Span<byte> aad = stackalloc byte[AeadFormat.AadLen];
        AeadFormat.BuildNonce(nonce, _noncePrefix, _frameIndex);
        AeadFormat.BuildAad(aad, _header, _frameIndex, isFinal);

        var plaintext = _buffer.AsSpan(0, _bufLen);
        _gcm!.Encrypt(nonce, plaintext,
                      _frame.AsSpan(0, _bufLen),
                      _frame.AsSpan(_bufLen, AeadFormat.TagLen), aad);

        // The plaintext window is dead the moment it is sealed — do not leave it for the GC to hand on.
        CryptographicOperations.ZeroMemory(plaintext);
        return _bufLen + AeadFormat.TagLen;
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (_gcm is not null)
        {
            try { await FinishAsync().ConfigureAwait(false); }
            finally
            {
                Wipe();
                if (!_leaveOpen) await _inner.DisposeAsync().ConfigureAwait(false);
            }
        }
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || _gcm is null) { base.Dispose(disposing); return; }
        try { FinishAsync().GetAwaiter().GetResult(); }
        finally
        {
            Wipe();
            if (!_leaveOpen) _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private void Wipe()
    {
        _gcm?.Dispose();
        _gcm = null;
        CryptographicOperations.ZeroMemory(_dek);
        CryptographicOperations.ZeroMemory(_buffer);
        CryptographicOperations.ZeroMemory(_frame);
    }
}
