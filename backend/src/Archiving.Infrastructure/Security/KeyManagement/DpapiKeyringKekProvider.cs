using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// KEK store for a self-hosted Windows install, where there is no cloud KMS to call.
///
/// <para>KEK versions live in a keyring file encrypted with DPAPI at <see cref="DataProtectionScope.LocalMachine"/>,
/// so the file is meaningless if copied to another machine — it is bound to this host's master key. That
/// is a real boundary against "someone walked off with the disk", which is the threat a single key sitting
/// in <c>appsettings.json</c> does nothing about. It is <b>not</b> an HSM: an attacker running as the
/// service account on this host can still ask DPAPI to unprotect it. Where that matters, point
/// <c>Kms:Provider</c> at <see cref="ExternalKmsKekProvider"/> and a Vault/HSM instance.</para>
///
/// <para>Wrapping is AES-256-GCM with a random 12-byte nonce, and the KEK version id is bound as AAD so a
/// wrapped DEK cannot be replayed against a different KEK version.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiKeyringKekProvider : IKekProvider
{
    private const int KeyLen = 32;
    private const int NonceLen = 12;
    private const int TagLen = 16;

    private static readonly byte[] Entropy = "Archiving.Kms.Keyring.v1"u8.ToArray();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private Keyring? _cache;

    public DpapiKeyringKekProvider(IOptions<KmsOptions> options)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "The DPAPI keyring provider requires Windows. Set Kms:Provider to ExternalKms on other platforms.");

        _path = options.Value.KeyringPath;
        var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public string Name => "DpapiKeyring";

    public async Task<string> GetActiveKeyIdAsync(CancellationToken ct = default)
        => (await LoadAsync(ct).ConfigureAwait(false)).ActiveKeyId;

    public async Task<byte[]> WrapAsync(string keyId, ReadOnlyMemory<byte> dek, CancellationToken ct = default)
    {
        if (dek.Length != KeyLen) throw new ArgumentException("DEK must be 32 bytes.", nameof(dek));

        var kek = await ResolveAsync(keyId, ct).ConfigureAwait(false);
        try
        {
            // Layout: nonce || ciphertext || tag
            var output = new byte[NonceLen + dek.Length + TagLen];
            RandomNumberGenerator.Fill(output.AsSpan(0, NonceLen));

            using var gcm = new AesGcm(kek, TagLen);
            gcm.Encrypt(
                output.AsSpan(0, NonceLen),
                dek.Span,
                output.AsSpan(NonceLen, dek.Length),
                output.AsSpan(NonceLen + dek.Length, TagLen),
                Encoding.UTF8.GetBytes(keyId));       // bind the version: no cross-version replay
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(kek); }
    }

    public async Task<byte[]> UnwrapAsync(string keyId, ReadOnlyMemory<byte> wrapped, CancellationToken ct = default)
    {
        if (wrapped.Length != NonceLen + KeyLen + TagLen)
            throw new CryptographicException("Wrapped key is malformed.");

        var kek = await ResolveAsync(keyId, ct).ConfigureAwait(false);
        try
        {
            var dek = new byte[KeyLen];
            using var gcm = new AesGcm(kek, TagLen);
            gcm.Decrypt(
                wrapped.Span[..NonceLen],
                wrapped.Span.Slice(NonceLen, KeyLen),
                wrapped.Span.Slice(NonceLen + KeyLen, TagLen),
                dek,
                Encoding.UTF8.GetBytes(keyId));
            return dek;
        }
        finally { CryptographicOperations.ZeroMemory(kek); }
    }

    public async Task<string> CreateKeyVersionAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var ring = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            var next = $"kek-{ring.Keys.Count + 1}";
            ring.Keys[next] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLen));
            ring.ActiveKeyId = next;
            ring.CreatedUtc[next] = DateTime.UtcNow;
            await SaveAsync(ring, ct).ConfigureAwait(false);
            _cache = ring;
            return next;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken ct = default)
        => (await LoadAsync(ct).ConfigureAwait(false)).Keys.Keys.ToList();

    public async Task RetireKeyVersionAsync(string keyId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var ring = await LoadUnlockedAsync(ct).ConfigureAwait(false);
            if (ring.ActiveKeyId == keyId)
                throw new InvalidOperationException("Refusing to retire the active KEK version. Rotate first.");
            if (!ring.Keys.Remove(keyId)) return;
            ring.CreatedUtc.Remove(keyId);
            await SaveAsync(ring, ct).ConfigureAwait(false);
            _cache = ring;
        }
        finally { _gate.Release(); }
    }

    private async Task<byte[]> ResolveAsync(string keyId, CancellationToken ct)
    {
        var ring = await LoadAsync(ct).ConfigureAwait(false);
        if (!ring.Keys.TryGetValue(keyId, out var b64))
            throw new CryptographicException($"KEK version '{keyId}' is not present in the keyring.");
        return Convert.FromBase64String(b64);
    }

    private async Task<Keyring> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return _cache ??= await LoadUnlockedAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<Keyring> LoadUnlockedAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            // First run bootstraps its own KEK, so there is no default key anyone can forget to change.
            var ring = new Keyring { ActiveKeyId = "kek-1" };
            ring.Keys["kek-1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLen));
            ring.CreatedUtc["kek-1"] = DateTime.UtcNow;
            await SaveAsync(ring, ct).ConfigureAwait(false);
            return ring;
        }

        var protectedBytes = await File.ReadAllBytesAsync(_path, ct).ConfigureAwait(false);
        var json = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        try
        {
            return JsonSerializer.Deserialize<Keyring>(json)
                   ?? throw new CryptographicException("Keyring is empty or unreadable.");
        }
        finally { CryptographicOperations.ZeroMemory(json); }
    }

    private async Task SaveAsync(Keyring ring, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(ring);
        try
        {
            var sealedBytes = ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine);
            // Write-then-replace: a crash mid-write must never leave a truncated keyring, which would
            // strand every object in the archive behind an unreadable KEK.
            var tmp = _path + ".tmp";
            await File.WriteAllBytesAsync(tmp, sealedBytes, ct).ConfigureAwait(false);
            File.Move(tmp, _path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(json); }
    }

    private sealed class Keyring
    {
        public string ActiveKeyId { get; set; } = "kek-1";
        public Dictionary<string, string> Keys { get; set; } = [];
        public Dictionary<string, DateTime> CreatedUtc { get; set; } = [];
    }
}
