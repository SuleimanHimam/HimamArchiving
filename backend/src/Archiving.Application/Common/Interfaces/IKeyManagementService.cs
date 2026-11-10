using System.Security.Cryptography;

namespace Archiving.Application.Common.Interfaces;

/// <summary>A freshly minted data key: the plaintext DEK for immediate use, and the wrapped form to
/// persist. <b>Always use inside <c>using</c></b> — dispose zeroes the plaintext copy, which is the
/// only thing standing between a key and a heap dump.</summary>
public sealed class DataKey : IDisposable
{
    private readonly byte[] _plaintext;
    private bool _disposed;

    public DataKey(byte[] plaintext, byte[] wrapped, string kekId)
    {
        _plaintext = plaintext;
        Wrapped = wrapped;
        KekId = kekId;
    }

    /// <summary>The raw 32-byte AES-256 key. Never persist, never log, never return over the wire.</summary>
    public ReadOnlySpan<byte> Plaintext
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _plaintext;
        }
    }

    /// <summary>The DEK sealed by the KEK. This is what goes in the metadata index.</summary>
    public byte[] Wrapped { get; }

    /// <summary>Identifies the KEK version that wrapped it, so rotation can find what still needs re-wrapping.</summary>
    public string KekId { get; }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_plaintext);
        _disposed = true;
    }
}

/// <summary>
/// Envelope encryption boundary. Data encryption keys (DEKs) are per-object and short-lived in memory;
/// key encryption keys (KEKs) live in a KMS and never leave it in the clear.
///
/// <para>The point of the split: rotating a KEK re-wraps a few rows of key material in the metadata
/// index and touches <b>none</b> of the object bytes on disk, and destroying one DEK crypto-shreds
/// exactly one document without disturbing any other.</para>
/// </summary>
public interface IKeyManagementService
{
    /// <summary>Identifier of the KEK version new objects are currently wrapped under.</summary>
    string ActiveKekId { get; }

    /// <summary>Mints a fresh random DEK and returns it wrapped under the active KEK.</summary>
    Task<DataKey> GenerateDataKeyAsync(CancellationToken ct = default);

    /// <summary>Unwraps a stored DEK. The caller owns the returned buffer and must zero it — prefer
    /// <see cref="UseDataKeyAsync{T}"/>, which does that for you even when the body throws.</summary>
    Task<byte[]> UnwrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default);

    /// <summary>Re-seals a DEK under the active KEK. Used by rotation; a no-op result when
    /// <paramref name="kekId"/> is already current.</summary>
    Task<(byte[] Wrapped, string KekId)> RewrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default);

    /// <summary>Creates a new KEK version and makes it active. Existing objects keep working: their rows
    /// still name the KEK that wrapped them until rotation re-wraps them.</summary>
    Task<string> CreateKekVersionAsync(CancellationToken ct = default);

    /// <summary>Confirms the KMS is reachable and the active KEK is usable — for the health endpoint and
    /// the startup guard. Never throws; reports.</summary>
    Task<KmsHealth> CheckHealthAsync(CancellationToken ct = default);
}

public sealed record KmsHealth(bool Healthy, string Provider, string ActiveKekId, int KnownKeyVersions, string? Error);

public static class KeyManagementExtensions
{
    /// <summary>Unwraps a DEK, runs <paramref name="body"/> with it, and zeroes it afterwards whatever
    /// happens. This is the only shape in which unwrapped key material should be handled.</summary>
    public static async Task<T> UseDataKeyAsync<T>(
        this IKeyManagementService kms, string kekId, byte[] wrapped,
        Func<ReadOnlyMemory<byte>, Task<T>> body, CancellationToken ct = default)
    {
        var dek = await kms.UnwrapAsync(kekId, wrapped, ct).ConfigureAwait(false);
        try { return await body(dek).ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }
}
