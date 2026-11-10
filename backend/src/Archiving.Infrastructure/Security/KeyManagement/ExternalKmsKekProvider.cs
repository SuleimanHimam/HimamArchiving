using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// KEK provider backed by an external KMS over its HTTP API, shaped to HashiCorp Vault's Transit engine
/// (<c>/keys/{name}</c>, <c>/encrypt/{name}</c>, <c>/decrypt/{name}</c>, <c>/keys/{name}/rotate</c>).
///
/// <para>This is the deployment to prefer wherever the threat model includes the archive host itself:
/// the KEK never enters this process at all — DEKs are shipped to the KMS to be wrapped and unwrapped,
/// and every operation is logged and revocable on the KMS side. Azure Key Vault and AWS KMS expose the
/// same wrap/unwrap primitive; adapting means changing the three request shapes below, not the design.</para>
///
/// <para>Transport is pinned to HTTPS in the constructor. A KMS reached over cleartext HTTP would hand
/// every data key to anyone on the path, which defeats the entire envelope scheme.</para>
/// </summary>
public sealed class ExternalKmsKekProvider : IKekProvider
{
    private readonly HttpClient _http;
    private readonly string _keyName;

    public ExternalKmsKekProvider(HttpClient http, IOptions<KmsOptions> options)
    {
        var o = options.Value;

        if (string.IsNullOrWhiteSpace(o.Endpoint))
            throw new InvalidOperationException("Kms:Endpoint is required when Kms:Provider is ExternalKms.");
        if (!Uri.TryCreate(o.Endpoint.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("Kms:Endpoint is not a valid absolute URI.");
        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kms:Endpoint must be HTTPS — data keys travel over this channel.");
        if (string.IsNullOrWhiteSpace(o.KeyName))
            throw new InvalidOperationException("Kms:KeyName is required when Kms:Provider is ExternalKms.");

        _http = http;
        _http.BaseAddress = baseUri;
        _keyName = o.KeyName;

        if (!string.IsNullOrWhiteSpace(o.Token))
            _http.DefaultRequestHeaders.Add("X-Vault-Token", o.Token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Name => "ExternalKms";

    public async Task<string> GetActiveKeyIdAsync(CancellationToken ct = default)
    {
        var info = await _http.GetFromJsonAsync<KeyInfoResponse>($"keys/{_keyName}", ct).ConfigureAwait(false)
                   ?? throw new CryptographicException("KMS returned no key metadata.");
        return $"{_keyName}:v{info.Data?.LatestVersion ?? 1}";
    }

    public async Task<byte[]> WrapAsync(string keyId, ReadOnlyMemory<byte> dek, CancellationToken ct = default)
    {
        var res = await PostAsync<CipherResponse>($"encrypt/{_keyName}",
            new { plaintext = Convert.ToBase64String(dek.Span) }, ct).ConfigureAwait(false);

        var ciphertext = res.Data?.Ciphertext
            ?? throw new CryptographicException("KMS returned no ciphertext.");
        return System.Text.Encoding.UTF8.GetBytes(ciphertext);
    }

    public async Task<byte[]> UnwrapAsync(string keyId, ReadOnlyMemory<byte> wrapped, CancellationToken ct = default)
    {
        var res = await PostAsync<PlainResponse>($"decrypt/{_keyName}",
            new { ciphertext = System.Text.Encoding.UTF8.GetString(wrapped.Span) }, ct).ConfigureAwait(false);

        var b64 = res.Data?.Plaintext
            ?? throw new CryptographicException("KMS returned no plaintext.");
        return Convert.FromBase64String(b64);
    }

    public async Task<string> CreateKeyVersionAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync($"keys/{_keyName}/rotate", content: null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await GetActiveKeyIdAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken ct = default)
    {
        var info = await _http.GetFromJsonAsync<KeyInfoResponse>($"keys/{_keyName}", ct).ConfigureAwait(false);
        var latest = info?.Data?.LatestVersion ?? 1;
        var min = info?.Data?.MinDecryptionVersion ?? 1;
        return Enumerable.Range(min, Math.Max(0, latest - min + 1)).Select(v => $"{_keyName}:v{v}").ToList();
    }

    /// <summary>Vault retires versions by raising <c>min_decryption_version</c>; anything below it can no
    /// longer be decrypted, which is exactly the permanent-destruction backstop we want.</summary>
    public async Task RetireKeyVersionAsync(string keyId, CancellationToken ct = default)
    {
        var version = ParseVersion(keyId);
        using var response = await _http.PostAsJsonAsync($"keys/{_keyName}/config",
            new { min_decryption_version = version + 1 }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static int ParseVersion(string keyId)
    {
        var idx = keyId.LastIndexOf(":v", StringComparison.Ordinal);
        return idx >= 0 && int.TryParse(keyId[(idx + 2)..], out var v) ? v : 1;
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false)
               ?? throw new CryptographicException("KMS returned an empty response.");
    }

    private sealed record KeyInfoResponse([property: JsonPropertyName("data")] KeyInfoData? Data);
    private sealed record KeyInfoData(
        [property: JsonPropertyName("latest_version")] int LatestVersion,
        [property: JsonPropertyName("min_decryption_version")] int MinDecryptionVersion);

    private sealed record CipherResponse([property: JsonPropertyName("data")] CipherData? Data);
    private sealed record CipherData([property: JsonPropertyName("ciphertext")] string Ciphertext);

    private sealed record PlainResponse([property: JsonPropertyName("data")] PlainData? Data);
    private sealed record PlainData([property: JsonPropertyName("plaintext")] string Plaintext);
}
