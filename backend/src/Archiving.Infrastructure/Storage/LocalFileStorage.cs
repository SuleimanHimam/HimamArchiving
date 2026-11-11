using System.Security.Cryptography;
using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Archiving.Infrastructure.Storage;

/// <summary>Disk-backed <see cref="IFileStorage"/>. Keys are <c>folder/yyyy/MM/{guid}{ext}</c> under
/// <c>Storage:RootPath</c>. When a 32-byte base64 <c>Storage:EncryptionKey</c> (the master KEK) is set,
/// each file is encrypted with its own random data key (DEK); the DEK is wrapped by the master and stored
/// in the file header. This enables <b>crypto-shredding</b>: destroying one file's wrapped DEK renders its
/// ciphertext permanently unrecoverable without touching any other file. On-disk layout for a new file:
/// <c>AENC2 || ivWrap(16) || wrappedDEK(48) || ivData(16) || ciphertext</c>. Legacy <c>AENC1</c> (global-key)
/// and plaintext files remain readable; they crypto-shred via secure overwrite. The recorded checksum is
/// always over the plaintext so fixity/OCR/preservation/download are unaffected.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private static readonly byte[] Magic1 = "AENC1\0"u8.ToArray();  // legacy: global-key encryption
    private static readonly byte[] Magic2 = "AENC2\0"u8.ToArray();  // per-file wrapped data key
    private const int IvLen = 16;
    private const int WrappedDekLen = 48;  // AES-CBC(32-byte DEK) with PKCS7 padding

    private readonly string _root;
    private readonly byte[]? _key;

    public LocalFileStorage(IConfiguration config)
    {
        // appsettings ships Storage:RootPath as "", so `??` alone leaves an empty path here and
        // Directory.CreateDirectory throws. Treat blank as unset, matching ResolveRoot in
        // EncryptedFileServerStorage.
        var root = config["Storage:RootPath"];
        _root = string.IsNullOrWhiteSpace(root) ? Path.Combine(AppContext.BaseDirectory, "storage") : root;
        Directory.CreateDirectory(_root);

        var b64 = config["Storage:EncryptionKey"];
        if (!string.IsNullOrWhiteSpace(b64))
        {
            _key = Convert.FromBase64String(b64);
            if (_key.Length != 32) throw new InvalidOperationException("Storage:EncryptionKey must be 32 bytes (base64).");
        }
    }

    public bool EncryptionEnabled => _key is not null;

    public async Task<StoredFile> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        var key = $"{folder}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}".Replace('\\', '/');
        var fullPath = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long size;
        string checksum;

        if (_key is null)
        {
            using var sha = SHA256.Create();
            await using var file = File.Create(fullPath);
            await using var crypto = new CryptoStream(file, sha, CryptoStreamMode.Write);
            size = await CopyHashAsync(content, crypto, null, ct);
            await crypto.FlushFinalBlockAsync(ct);
            checksum = Convert.ToHexString(sha.Hash!);
        }
        else
        {
            // Per-file data key, wrapped by the master key and embedded in the header.
            var dek = RandomNumberGenerator.GetBytes(32);
            var ivWrap = RandomNumberGenerator.GetBytes(IvLen);
            var wrappedDek = WrapKey(dek, ivWrap);

            using var aes = Aes.Create();
            aes.Key = dek; aes.GenerateIV();
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var file = File.Create(fullPath);
            await file.WriteAsync(Magic2, ct);
            await file.WriteAsync(ivWrap, ct);
            await file.WriteAsync(wrappedDek, ct);
            await file.WriteAsync(aes.IV, ct);
            await using (var enc = new CryptoStream(file, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                size = await CopyHashAsync(content, enc, sha, ct);
                await enc.FlushFinalBlockAsync(ct);
            }
            CryptographicOperations.ZeroMemory(dek);
            checksum = Convert.ToHexString(sha.GetHashAndReset());
        }

        return new StoredFile(key, size, checksum);
    }

    public async Task<Stream?> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath)) return null;

        var fs = File.OpenRead(fullPath);
        var header = new byte[Magic2.Length];
        var read = await fs.ReadAsync(header, ct);

        if (read == Magic2.Length && header.AsSpan().SequenceEqual(Magic2))
        {
            if (_key is null) { fs.Dispose(); throw new InvalidOperationException("File is encrypted but no key is configured."); }
            var ivWrap = new byte[IvLen]; await fs.ReadExactlyAsync(ivWrap, ct);
            var wrapped = new byte[WrappedDekLen]; await fs.ReadExactlyAsync(wrapped, ct);
            var dek = UnwrapKey(wrapped, ivWrap);
            var ivData = new byte[IvLen]; await fs.ReadExactlyAsync(ivData, ct);
            var aes = Aes.Create();
            aes.Key = dek; aes.IV = ivData;
            return new CryptoStream(fs, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: false);
        }

        if (_key is not null && read >= Magic1.Length && header.AsSpan(0, Magic1.Length).SequenceEqual(Magic1))
        {
            // Legacy AENC1: MAGIC1 || IV || ciphertext (global key).
            fs.Position = Magic1.Length;
            var iv = new byte[IvLen]; await fs.ReadExactlyAsync(iv, ct);
            var aes = Aes.Create();
            aes.Key = _key; aes.IV = iv;
            return new CryptoStream(fs, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: false);
        }

        fs.Position = 0;
        return fs; // plaintext
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task<string> CryptoShredAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath)) return "AlreadyGone";

        // If it's a per-file-keyed file, overwrite the wrapped-DEK region so the key can never be unwrapped.
        bool shredded = false;
        await using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite))
        {
            var header = new byte[Magic2.Length];
            var read = await fs.ReadAsync(header, ct);
            if (read == Magic2.Length && header.AsSpan().SequenceEqual(Magic2))
            {
                var noise = RandomNumberGenerator.GetBytes(IvLen + WrappedDekLen);
                fs.Position = Magic2.Length;            // start of ivWrap + wrappedDEK
                await fs.WriteAsync(noise, ct);
                await fs.FlushAsync(ct);
                shredded = true;
            }
        }

        if (shredded)
        {
            File.Delete(fullPath);   // key material gone; remove the (now-undecryptable) bytes too
            return "CryptoShred";
        }
        // Legacy/plaintext: no per-file key to destroy — fall back to overwrite.
        return await SecureOverwriteAsync(storageKey, 3, ct);
    }

    public async Task<string> SecureOverwriteAsync(string storageKey, int passes = 3, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath)) return "AlreadyGone";

        var len = new FileInfo(fullPath).Length;
        await using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Write))
        {
            var buf = new byte[81920];
            for (var p = 0; p < Math.Max(1, passes); p++)
            {
                fs.Position = 0;
                long written = 0;
                while (written < len)
                {
                    RandomNumberGenerator.Fill(buf);
                    var n = (int)Math.Min(buf.Length, len - written);
                    await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    written += n;
                }
                await fs.FlushAsync(ct);
            }
            fs.SetLength(0); // truncate
        }
        File.Delete(fullPath);
        return "SecureOverwrite";
    }

    private byte[] WrapKey(byte[] dek, byte[] ivWrap)
    {
        using var aes = Aes.Create();
        aes.Key = _key!;
        return aes.EncryptCbc(dek, ivWrap);
    }

    private byte[] UnwrapKey(byte[] wrapped, byte[] ivWrap)
    {
        using var aes = Aes.Create();
        aes.Key = _key!;
        return aes.DecryptCbc(wrapped, ivWrap);
    }

    private static async Task<long> CopyHashAsync(Stream src, Stream dst, IncrementalHash? sha, CancellationToken ct)
    {
        var buf = new byte[81920];
        long total = 0; int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            sha?.AppendData(buf, 0, n);
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            total += n;
        }
        return total;
    }

    private string ResolvePath(string storageKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(_root);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");
        return combined;
    }
}
