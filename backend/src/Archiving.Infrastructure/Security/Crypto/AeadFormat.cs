using System.Buffers.Binary;

namespace Archiving.Infrastructure.Security.Crypto;

/// <summary>
/// On-disk container format for archived objects — <b>AENC3</b>, chunked AES-256-GCM.
///
/// <para><b>Layout</b></para>
/// <code>
/// header (24 bytes, cleartext but authenticated as AAD on every frame)
///   [0..5]   magic        "AENC3\0"
///   [6]      version      0x01
///   [7]      flags        reserved, 0x00
///   [8..11]  chunkSize    uint32 big-endian — plaintext bytes per frame
///   [12..19] noncePrefix  8 random bytes, unique per object
///   [20..23] reserved     0x00000000
/// frames (one or more, in order)
///   ciphertext[n] || tag[16]      n == chunkSize for every frame but the last
/// </code>
///
/// <para><b>Why chunked.</b> <see cref="System.Security.Cryptography.AesGcm"/> is a one-shot API: it
/// will not authenticate a stream it has not seen in full. Archived objects are far too large to hold
/// in memory, so the object is split into independently sealed frames and the framing itself is
/// authenticated — which is what stops an attacker reordering, duplicating, splicing or truncating
/// frames that are each individually valid.</para>
///
/// <para><b>Nonce discipline.</b> Nonce = <c>noncePrefix(8) || frameIndex(4, big-endian)</c>. The prefix
/// is fresh CSPRNG output per object and the DEK is fresh per object, so a (key, nonce) pair is never
/// reused — the one failure that breaks GCM catastrophically. 2^32 frames × 1 MiB = 4 PiB per object.</para>
///
/// <para><b>AAD.</b> <c>header(24) || frameIndex(4, big-endian) || isFinal(1)</c>. Binding the header
/// pins the declared chunk size and nonce prefix; binding the index pins order; binding the final flag
/// is what makes truncation detectable — see <see cref="AeadWritingStream"/>.</para>
/// </summary>
public static class AeadFormat
{
    public static readonly byte[] Magic = "AENC3\0"u8.ToArray();

    public const byte Version = 0x01;
    public const int HeaderLen = 24;
    public const int TagLen = 16;          // AES-GCM 128-bit tag
    public const int NoncePrefixLen = 8;
    public const int NonceLen = 12;        // 8-byte prefix + 4-byte counter
    public const int KeyLen = 32;          // AES-256
    public const int AadLen = HeaderLen + 4 + 1;

    /// <summary>Default plaintext bytes per frame. 1 MiB keeps per-frame overhead at 0.0015% while
    /// bounding the buffer a single request pins in memory.</summary>
    public const int DefaultChunkSize = 1024 * 1024;

    public const int MinChunkSize = 16 * 1024;
    public const int MaxChunkSize = 16 * 1024 * 1024;

    /// <summary>True when <paramref name="header"/> opens with the AENC3 magic.</summary>
    public static bool IsAenc3(ReadOnlySpan<byte> header) =>
        header.Length >= HeaderLen && header[..Magic.Length].SequenceEqual(Magic);

    public static void WriteHeader(Span<byte> dest, int chunkSize, ReadOnlySpan<byte> noncePrefix)
    {
        if (dest.Length < HeaderLen) throw new ArgumentException("Header buffer too small.", nameof(dest));
        if (noncePrefix.Length != NoncePrefixLen) throw new ArgumentException("Bad nonce prefix.", nameof(noncePrefix));

        dest[..HeaderLen].Clear();
        Magic.CopyTo(dest);
        dest[6] = Version;
        dest[7] = 0;
        BinaryPrimitives.WriteUInt32BigEndian(dest[8..12], (uint)chunkSize);
        noncePrefix.CopyTo(dest[12..20]);
    }

    /// <summary>Parses and validates a header. Throws <see cref="CryptographicFormatException"/> rather
    /// than returning a partly-filled result, so a malformed object can never be read as plaintext.</summary>
    public static (int ChunkSize, byte[] NoncePrefix) ReadHeader(ReadOnlySpan<byte> header)
    {
        if (!IsAenc3(header)) throw new CryptographicFormatException("Not an AENC3 object.");
        if (header[6] != Version) throw new CryptographicFormatException($"Unsupported container version {header[6]}.");

        var chunkSize = (int)BinaryPrimitives.ReadUInt32BigEndian(header[8..12]);
        if (chunkSize is < MinChunkSize or > MaxChunkSize)
            throw new CryptographicFormatException("Declared chunk size is out of range.");

        return (chunkSize, header[12..20].ToArray());
    }

    /// <summary>Nonce for one frame: <c>noncePrefix || frameIndex</c>.</summary>
    public static void BuildNonce(Span<byte> dest, ReadOnlySpan<byte> noncePrefix, uint frameIndex)
    {
        noncePrefix.CopyTo(dest);
        BinaryPrimitives.WriteUInt32BigEndian(dest[NoncePrefixLen..NonceLen], frameIndex);
    }

    /// <summary>Associated data for one frame: <c>header || frameIndex || isFinal</c>.</summary>
    public static void BuildAad(Span<byte> dest, ReadOnlySpan<byte> header, uint frameIndex, bool isFinal)
    {
        header[..HeaderLen].CopyTo(dest);
        BinaryPrimitives.WriteUInt32BigEndian(dest[HeaderLen..(HeaderLen + 4)], frameIndex);
        dest[HeaderLen + 4] = isFinal ? (byte)1 : (byte)0;
    }
}

/// <summary>The stored object is not a well-formed container. Deliberately distinct from
/// <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/> so callers can tell
/// "this is not our format" from "this has been tampered with" — both are refusals, but only the second
/// is a fixity incident.</summary>
public sealed class CryptographicFormatException(string message) : Exception(message);
