using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// A KEK read straight out of configuration. This is what the audit flagged as a finding, kept only so a
/// developer can run the stack without a keyring or a Vault instance — and it <b>refuses to construct
/// outside Development</b>, so it cannot quietly become the production posture.
///
/// <para>It is deliberately loud rather than convenient: no default key, no silent fallback, and a name
/// that shows up as "Insecure" in the health endpoint and every log line.</para>
/// </summary>
public sealed class InsecureConfigKekProvider : IKekProvider
{
    private const int KeyLen = 32;
    private const int NonceLen = 12;
    private const int TagLen = 16;
    private const string OnlyKeyId = "insecure-config-1";

    private readonly byte[] _kek;

    public InsecureConfigKekProvider(IOptions<KmsOptions> options, IHostEnvironment env)
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "Kms:Provider 'Insecure' holds the key encryption key in a configuration file and is refused " +
                $"outside Development (current environment: {env.EnvironmentName}). " +
                "Use 'DpapiKeyring' on a Windows host, or 'ExternalKms' against a Vault/HSM instance.");

        var b64 = options.Value.InsecureKey;
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException("Kms:InsecureKey is required when Kms:Provider is Insecure.");

        _kek = Convert.FromBase64String(b64);
        if (_kek.Length != KeyLen)
            throw new InvalidOperationException("Kms:InsecureKey must decode to exactly 32 bytes (AES-256).");
    }

    public string Name => "Insecure";

    public Task<string> GetActiveKeyIdAsync(CancellationToken ct = default) => Task.FromResult(OnlyKeyId);

    public Task<byte[]> WrapAsync(string keyId, ReadOnlyMemory<byte> dek, CancellationToken ct = default)
    {
        var output = new byte[NonceLen + dek.Length + TagLen];
        RandomNumberGenerator.Fill(output.AsSpan(0, NonceLen));
        using var gcm = new AesGcm(_kek, TagLen);
        gcm.Encrypt(output.AsSpan(0, NonceLen), dek.Span, output.AsSpan(NonceLen, dek.Length),
                    output.AsSpan(NonceLen + dek.Length, TagLen), Encoding.UTF8.GetBytes(keyId));
        return Task.FromResult(output);
    }

    public Task<byte[]> UnwrapAsync(string keyId, ReadOnlyMemory<byte> wrapped, CancellationToken ct = default)
    {
        if (wrapped.Length != NonceLen + KeyLen + TagLen) throw new CryptographicException("Wrapped key is malformed.");
        var dek = new byte[KeyLen];
        using var gcm = new AesGcm(_kek, TagLen);
        gcm.Decrypt(wrapped.Span[..NonceLen], wrapped.Span.Slice(NonceLen, KeyLen),
                    wrapped.Span.Slice(NonceLen + KeyLen, TagLen), dek, Encoding.UTF8.GetBytes(keyId));
        return Task.FromResult(dek);
    }

    public Task<string> CreateKeyVersionAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("The Insecure provider holds one static key and cannot rotate. Use DpapiKeyring or ExternalKms.");

    public Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([OnlyKeyId]);

    public Task RetireKeyVersionAsync(string keyId, CancellationToken ct = default) =>
        throw new NotSupportedException("The Insecure provider cannot retire its only key.");
}
