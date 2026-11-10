namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// The KMS boundary proper: wrap and unwrap of data keys by a key-encryption key that the application
/// never sees in the clear.
///
/// <para>Implementations are expected to keep KEK material inside a protected store — DPAPI, an HSM, a
/// cloud KMS — and to expose only these operations. Nothing here returns a KEK.</para>
/// </summary>
public interface IKekProvider
{
    /// <summary>Short name for logs and the health endpoint.</summary>
    string Name { get; }

    /// <summary>Identifier of the KEK version new wraps use, e.g. <c>kek-3</c>.</summary>
    Task<string> GetActiveKeyIdAsync(CancellationToken ct = default);

    /// <summary>Seals a 32-byte DEK under the named KEK version.</summary>
    Task<byte[]> WrapAsync(string keyId, ReadOnlyMemory<byte> dek, CancellationToken ct = default);

    /// <summary>Opens a sealed DEK. Throws when the KEK version is unknown — which, after a shred-and-retire
    /// cycle, is itself a valid permanent-destruction outcome.</summary>
    Task<byte[]> UnwrapAsync(string keyId, ReadOnlyMemory<byte> wrapped, CancellationToken ct = default);

    /// <summary>Generates a new KEK version and makes it active. Older versions stay available for
    /// unwrapping until rotation has re-wrapped everything under them.</summary>
    Task<string> CreateKeyVersionAsync(CancellationToken ct = default);

    /// <summary>Every KEK version this provider can still unwrap with.</summary>
    Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken ct = default);

    /// <summary>Permanently removes a retired KEK version. Only safe once no row references it — the last
    /// backstop of a crypto-shred, and irreversible.</summary>
    Task RetireKeyVersionAsync(string keyId, CancellationToken ct = default);
}
