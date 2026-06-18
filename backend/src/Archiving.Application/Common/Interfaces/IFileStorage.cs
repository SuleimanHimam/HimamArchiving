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
}
