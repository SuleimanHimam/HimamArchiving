using System.Security.Cryptography;

namespace Archiving.Infrastructure.Services;

/// <summary>Computes the fixity digest of a stored file. Uppercase hex, matching the value the
/// storage layer records at ingest, so checks compare like-for-like.</summary>
public static class FileHasher
{
    public static async Task<string> Sha256HexAsync(Stream content, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(content, ct);
        return Convert.ToHexString(hash);
    }
}
