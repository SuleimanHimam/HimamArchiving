using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Archiving.Application.Common.Interfaces;

namespace Archiving.Infrastructure.Security.Crypto;

/// <summary>
/// AES-256-GCM over a fresh data key per value, the data key wrapped by the active KEK.
///
/// Layout (version 1):
/// <c>[1:version][1:kekIdLen][kekId][2:wrappedLen][wrapped][12:nonce][16:tag][ciphertext]</c>.
/// The version byte and KEK id are bound as associated data, so neither can be swapped without the
/// tag failing. Values are short (an id, a phone, an IBAN), so the per-value key costs one wrap/unwrap.
/// </summary>
public sealed class SensitiveDataProtector(IKeyManagementService kms) : ISensitiveDataProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12, TagSize = 16;

    public async Task<byte[]> ProtectAsync(string plain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plain);
        using var key = await kms.GenerateDataKeyAsync(ct);
        var kekId = Encoding.UTF8.GetBytes(key.KekId);
        if (kekId.Length > byte.MaxValue || key.Wrapped.Length > ushort.MaxValue)
            throw new CryptographicException("Key identifier or wrapped key too large.");

        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key.Plaintext, TagSize))
            aes.Encrypt(nonce, data, cipher, tag, Aad(kekId));
        CryptographicOperations.ZeroMemory(data);

        var blob = new byte[1 + 1 + kekId.Length + 2 + key.Wrapped.Length + NonceSize + TagSize + cipher.Length];
        var span = blob.AsSpan();
        span[0] = Version;
        span[1] = (byte)kekId.Length;
        kekId.CopyTo(span[2..]);
        var pos = 2 + kekId.Length;
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], (ushort)key.Wrapped.Length);
        pos += 2;
        key.Wrapped.CopyTo(span[pos..]);
        pos += key.Wrapped.Length;
        nonce.CopyTo(span[pos..]);
        pos += NonceSize;
        tag.CopyTo(span[pos..]);
        pos += TagSize;
        cipher.CopyTo(span[pos..]);
        return blob;
    }

    public async Task<string> UnprotectAsync(byte[] protectedValue, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        try
        {
            var span = protectedValue.AsSpan();
            if (span.Length < 2 || span[0] != Version) throw new CryptographicException("Unknown format.");
            int kekLen = span[1];
            var kekIdBytes = span.Slice(2, kekLen).ToArray();
            var pos = 2 + kekLen;
            int wrappedLen = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
            pos += 2;
            var wrapped = span.Slice(pos, wrappedLen).ToArray();
            pos += wrappedLen;
            var nonce = span.Slice(pos, NonceSize).ToArray();
            pos += NonceSize;
            var tag = span.Slice(pos, TagSize).ToArray();
            pos += TagSize;
            var cipher = span[pos..].ToArray();

            return await kms.UseDataKeyAsync(Encoding.UTF8.GetString(kekIdBytes), wrapped, dek =>
            {
                var plain = new byte[cipher.Length];
                try
                {
                    using var aes = new AesGcm(dek.Span, TagSize);
                    aes.Decrypt(nonce, cipher, tag, plain, Aad(kekIdBytes));
                    return Task.FromResult(Encoding.UTF8.GetString(plain));
                }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }, ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new CryptographicException("Truncated value.", ex);
        }
    }

    private static byte[] Aad(byte[] kekId)
    {
        var aad = new byte[1 + kekId.Length];
        aad[0] = Version;
        kekId.CopyTo(aad, 1);
        return aad;
    }
}
