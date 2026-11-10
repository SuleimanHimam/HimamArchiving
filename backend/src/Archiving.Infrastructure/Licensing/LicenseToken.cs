using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Archiving.Infrastructure.Licensing;

/// <summary>The signed contents of a licence. Serialised to JSON, base64url-encoded, and signed —
/// so a customer can read exactly what they were sold but cannot alter a byte of it.</summary>
public sealed record LicensePayload
{
    /// <summary>Vendor-side identifier for this issue, quoted in support and in the audit trail.</summary>
    [JsonPropertyName("id")]         public string  Id         { get; init; } = "";
    [JsonPropertyName("customer")]   public string  Customer   { get; init; } = "";
    /// <summary>Machine fingerprint this licence is bound to. <see cref="AnyMachine"/> means unbound —
    /// only ever issued for our own demo/dev boxes.</summary>
    [JsonPropertyName("machineId")]  public string  MachineId  { get; init; } = "";
    [JsonPropertyName("edition")]    public string  Edition    { get; init; } = "Standard";
    [JsonPropertyName("issuedAt")]   public DateTime IssuedAt  { get; init; }
    /// <summary>Null for a perpetual licence.</summary>
    [JsonPropertyName("expiresAt")]  public DateTime? ExpiresAt { get; init; }
    /// <summary>Days the product keeps running after expiry, degraded and warning. 0 = hard stop.</summary>
    [JsonPropertyName("graceDays")]  public int      GraceDays  { get; init; }
    /// <summary>Null = unlimited user accounts.</summary>
    [JsonPropertyName("maxUsers")]   public int?     MaxUsers   { get; init; }
    /// <summary>Empty = every module enabled. Otherwise an allow-list of module keys.</summary>
    [JsonPropertyName("features")]   public string[] Features   { get; init; } = [];
    [JsonPropertyName("notes")]      public string?  Notes      { get; init; }

    public const string AnyMachine = "*";
}

/// <summary>Reader/writer for the licence file format:
/// <c>ARCHLIC-1.&lt;base64url payload&gt;.&lt;base64url signature&gt;</c>.
/// <para>Signing is ECDSA P-256 / SHA-256: the private key stays with the vendor, the public key ships
/// inside the product, so a licence can be verified with no network call at the customer site.</para>
/// <para>The signature covers the encoded payload <i>text</i>, not the re-serialised object, so
/// verification never depends on JSON formatting matching byte for byte.</para></summary>
public static class LicenseToken
{
    public const string Prefix = "ARCHLIC-1";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Create(LicensePayload payload, ECDsa privateKey)
    {
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Json));
        var sig  = Base64Url(privateKey.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256));
        return $"{Prefix}.{body}.{sig}";
    }

    /// <summary>Verifies the signature and returns the payload, or null with a reason. Never throws on
    /// malformed input — a hand-edited licence file is an expected case, not an exceptional one.</summary>
    public static bool TryRead(string token, ECDsa publicKey, out LicensePayload? payload, out string error)
    {
        payload = null;
        error   = "";

        // Tolerate the wrapping a licence picks up in transit: comment lines, CRLF, stray whitespace.
        var compact = new string(token.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var parts   = compact.Split('.');
        if (parts.Length != 3 || parts[0] != Prefix)
        {
            error = "Not an Archiving licence key.";
            return false;
        }

        byte[] bodyBytes, sigBytes;
        try
        {
            bodyBytes = FromBase64Url(parts[1]);
            sigBytes  = FromBase64Url(parts[2]);
        }
        catch (FormatException)
        {
            error = "Licence key is damaged (bad encoding).";
            return false;
        }

        if (!publicKey.VerifyData(Encoding.ASCII.GetBytes(parts[1]), sigBytes, HashAlgorithmName.SHA256))
        {
            error = "Licence signature is not valid — the key was altered or was not issued by us.";
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(bodyBytes, Json);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null)
        {
            error = "Licence contents could not be read.";
            return false;
        }
        return true;
    }

    /// <summary>Reads the payload without checking the signature — for tooling that wants to display
    /// what a key claims. Never use this to decide whether the product may run.</summary>
    public static LicensePayload? PeekUnverified(string token)
    {
        var parts = new string(token.Where(c => !char.IsWhiteSpace(c)).ToArray()).Split('.');
        if (parts.Length != 3) return null;
        try { return JsonSerializer.Deserialize<LicensePayload>(FromBase64Url(parts[1]), Json); }
        catch { return null; }
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }
}
