using System.Security.Cryptography;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security.Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Storage;

/// <summary>
/// File-server object store with mandatory envelope encryption.
///
/// <para><b>Ingestion.</b> validate → mint a DEK from the KMS → stream the upload through AES-256-GCM
/// straight onto the file server, hashing the plaintext as it passes → move into place atomically →
/// record the object and its wrapped DEK in the metadata index. The cleartext exists only as a 1 MiB
/// window inside this process; nothing unencrypted is ever written to disk, not even transiently.</para>
///
/// <para><b>Retrieval.</b> look up the row → refuse if shredded → unwrap the DEK through the KMS →
/// open the container and stream verified plaintext to the caller. Access control happens <i>before</i>
/// this is called (see <c>SecureArchiveReader</c>); by the time a DEK is unwrapped the decision is made.</para>
///
/// <para><b>Legacy.</b> Objects written by <see cref="LocalFileStorage"/> (AENC1/AENC2/plaintext, old key
/// shape) stay readable through <paramref name="legacy"/> so an existing archive keeps working during
/// migration. New writes always use the new scheme; <c>ArchiveMigrationService</c> moves the rest.</para>
///
/// <para>Registered as a singleton to match the existing <see cref="IFileStorage"/> lifetime, so database
/// work goes through <paramref name="scopeFactory"/> rather than a captured scoped context.</para>
/// </summary>
public sealed class EncryptedFileServerStorage(
    IServiceScopeFactory scopeFactory,
    IKeyManagementService kms,
    LocalFileStorage legacy,
    IConfiguration config,
    ILogger<EncryptedFileServerStorage> logger) : IFileStorage
{
    private readonly string _root = ResolveRoot(config);
    private readonly int _chunkSize = ResolveChunkSize(config);

    /// <summary>Where the file server tree lives. A UNC path (<c>\\nas01\archive$\objects</c>) or a mapped
    /// SAN/NAS volume is expected in production; the service account needs Modify and nothing else.</summary>
    public string Root => _root;

    // ── Ingestion ────────────────────────────────────────────────────────────────────────────────

    public async Task<StoredFile> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var key = StorageKeys.NewKey(folder);
        var finalPath = StorageKeys.ResolvePath(_root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        // Staged in the destination directory so the commit is a same-volume rename: a crash leaves a
        // .part file to sweep up, never a half-written object that the index believes is whole.
        var stagingPath = finalPath + ".part";

        long size;
        string checksum;
        byte[] noncePrefix;

        using var dataKey = await kms.GenerateDataKeyAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var file = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                                   bufferSize: 81920, useAsync: true))
            await using (var sealer = new AeadWritingStream(file, dataKey.Plaintext, _chunkSize, leaveOpen: true))
            {
                (size, checksum) = await CopyHashingPlaintextAsync(content, sealer, ct).ConfigureAwait(false);
                await sealer.FinishAsync(ct).ConfigureAwait(false);
            }

            // After the writing handle is closed, not inside it: the staging stream is opened
            // FileShare.None, so re-opening the same path while it is still held fails the
            // whole upload with a sharing violation.
            noncePrefix = ReadNoncePrefix(stagingPath);

            File.Move(stagingPath, finalPath, overwrite: false);
        }
        catch
        {
            TryDelete(stagingPath);
            throw;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ArchivedObjects.Add(new ArchivedObject
            {
                StorageKey = key,
                SizeBytes = size,
                ChecksumSha256 = checksum,
                Algorithm = "AES-256-GCM",
                KekId = dataKey.KekId,
                WrappedDek = dataKey.Wrapped,
                NoncePrefix = noncePrefix,
                ChunkSize = _chunkSize,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Without its row the object is undecryptable anyway — leaving it would be an orphan that
            // storage accounting can never explain. Roll the write back.
            TryDelete(finalPath);
            throw;
        }

        return new StoredFile(key, size, checksum);
    }

    // ── Retrieval ────────────────────────────────────────────────────────────────────────────────

    public async Task<Stream?> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        if (!StorageKeys.IsWellFormed(storageKey))
            return await legacy.OpenAsync(storageKey, ct).ConfigureAwait(false);

        var record = await FindAsync(storageKey, ct).ConfigureAwait(false);
        if (record is null) return null;

        if (record.IsShredded)
        {
            logger.LogInformation("Refused read of crypto-shredded object {StorageKey}", storageKey);
            return null;   // destroyed on purpose — indistinguishable from absent, by design
        }
        if (record.WrappedDek is null) return null;

        var path = StorageKeys.ResolvePath(_root, storageKey);
        if (!File.Exists(path)) return null;

        var dek = await kms.UnwrapAsync(record.KekId, record.WrappedDek, ct).ConfigureAwait(false);
        try
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                      bufferSize: 81920, useAsync: true);
            try
            {
                // Owns the file handle from here: disposing the returned stream closes both and zeroes the key.
                return await AeadReadingStream.OpenAsync(file, dek, leaveOpen: false, ct).ConfigureAwait(false);
            }
            catch
            {
                await file.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }

    // ── Deletion and destruction ─────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (!StorageKeys.IsWellFormed(storageKey))
        {
            await legacy.DeleteAsync(storageKey, ct).ConfigureAwait(false);
            return;
        }

        TryDelete(StorageKeys.ResolvePath(_root, storageKey));

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ArchivedObjects.FirstOrDefaultAsync(o => o.StorageKey == storageKey, ct).ConfigureAwait(false);
        if (row is null) return;
        db.ArchivedObjects.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Destroys the object's data key, making its ciphertext permanently unrecoverable — the digital half
    /// of an ISO 15489 destruction.
    ///
    /// <para>The key is destroyed <b>in the metadata index</b>, in two writes: first the wrapped DEK is
    /// overwritten with random bytes, then nulled and stamped. Two writes rather than one because a single
    /// <c>SET NULL</c> can leave the old value legible in a database page or an undo segment; overwriting
    /// first gives the engine a same-length value to write over it.</para>
    ///
    /// <para>Destroying a key, not scrubbing a file, is the whole point: in-place overwrite does not
    /// reliably destroy bytes on an SSD, a copy-on-write volume or a NAS with snapshots — but a document
    /// whose only key is gone is unrecoverable regardless of how many copies of the ciphertext survive.
    /// The ciphertext is then deleted as housekeeping, not as the destruction mechanism.</para>
    /// </summary>
    public async Task<string> CryptoShredAsync(string storageKey, CancellationToken ct = default)
    {
        if (!StorageKeys.IsWellFormed(storageKey))
            return await legacy.CryptoShredAsync(storageKey, ct).ConfigureAwait(false);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();

        var row = await db.ArchivedObjects.FirstOrDefaultAsync(o => o.StorageKey == storageKey, ct).ConfigureAwait(false);
        if (row is null)
        {
            TryDelete(StorageKeys.ResolvePath(_root, storageKey));
            return "AlreadyGone";
        }
        if (row.IsShredded) return "AlreadyShredded";

        row.WrappedDek = RandomNumberGenerator.GetBytes(row.WrappedDek?.Length ?? 60);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        row.WrappedDek = null;
        row.NoncePrefix = null;
        row.ShreddedAtUtc = DateTime.UtcNow;
        row.ShreddedByUserId = currentUser?.UserId;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        TryDelete(StorageKeys.ResolvePath(_root, storageKey));

        logger.LogWarning("Crypto-shredded object {StorageKey} (KEK {KekId})", storageKey, row.KekId);
        return "CryptoShred";
    }

    /// <summary>Kept for the <see cref="IFileStorage"/> contract. For AENC3 objects the key destruction in
    /// <see cref="CryptoShredAsync"/> is strictly stronger than any number of overwrite passes, so this
    /// defers to it rather than pretending a scrub on modern storage means something.</summary>
    public async Task<string> SecureOverwriteAsync(string storageKey, int passes = 3, CancellationToken ct = default)
    {
        if (!StorageKeys.IsWellFormed(storageKey))
            return await legacy.SecureOverwriteAsync(storageKey, passes, ct).ConfigureAwait(false);

        return await CryptoShredAsync(storageKey, ct).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private async Task<ArchivedObject?> FindAsync(string storageKey, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ArchivedObjects.AsNoTracking()
            .FirstOrDefaultAsync(o => o.StorageKey == storageKey, ct).ConfigureAwait(false);
    }

    /// <summary>Copies plaintext into the sealer while hashing it, so fixity is over the document as the
    /// user sees it — a re-encryption under a new key must not change the recorded checksum.</summary>
    private static async Task<(long Size, string Checksum)> CopyHashingPlaintextAsync(Stream source, Stream sealer, CancellationToken ct)
    {
        var buffer = new byte[81920];
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;

        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                sha.AppendData(buffer, 0, read);
                await sealer.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                total += read;
            }
            return (total, Convert.ToHexString(sha.GetHashAndReset()));
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static byte[] ReadNoncePrefix(string path)
    {
        using var fs = File.OpenRead(path);
        var header = new byte[AeadFormat.HeaderLen];
        fs.ReadExactly(header);
        return header.AsSpan(12, AeadFormat.NoncePrefixLen).ToArray();
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not remove {Path}", path); }
    }

    private static string ResolveRoot(IConfiguration config)
    {
        var root = config["Storage:RootPath"];
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(AppContext.BaseDirectory, "storage");
        Directory.CreateDirectory(root);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    private static int ResolveChunkSize(IConfiguration config)
    {
        var configured = config.GetValue<int?>("Storage:ChunkSizeBytes") ?? AeadFormat.DefaultChunkSize;
        return Math.Clamp(configured, AeadFormat.MinChunkSize, AeadFormat.MaxChunkSize);
    }
}
