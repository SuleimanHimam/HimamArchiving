namespace Archiving.Application.Common.Interfaces;

/// <summary>Result of persisting a file to object storage.</summary>
public sealed record StoredFile(string StorageKey, long SizeBytes, string Checksum);

/// <summary>Abstraction over the binary store (local disk for MVP; S3/MinIO in production).</summary>
public interface IFileStorage
{
    /// <summary>Persists a stream under a new key derived from <paramref name="folder"/> and <paramref name="fileName"/>.</summary>
    Task<StoredFile> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Opens a previously stored file for reading. Null if the key no longer exists.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Crypto-shred: irreversibly destroy this file's wrapped data key (and its bytes), so the
    /// ciphertext can never be decrypted again. Falls back to <see cref="SecureOverwriteAsync"/> for
    /// legacy/plaintext files. Returns the method actually applied.</summary>
    Task<string> CryptoShredAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Overwrite the file with random data (<paramref name="passes"/> times), truncate, and delete.</summary>
    Task<string> SecureOverwriteAsync(string storageKey, int passes = 3, CancellationToken ct = default);
}
