namespace Archiving.Infrastructure.Licensing;

public sealed class LicenseOptions
{
    public const string SectionName = "License";

    /// <summary>Where the licence file lives. Relative paths resolve against the content root, so the
    /// default puts <c>license.lic</c> next to the binary — a copy-and-start deployment.</summary>
    public string Path { get; set; } = "license.lic";

    /// <summary>Vendor signing public key, base64 SPKI (the value <c>licensegen keygen</c> prints).
    /// Config is checked first so a key rotation is a config change rather than a rebuild; if it is
    /// blank, <see cref="EmbeddedPublicKey"/> is used.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>Compiled-in fallback signing key. Paste the vendor public key here for a release
    /// build: config can be edited on the customer's server, this cannot.
    /// <para>While both this and <see cref="PublicKey"/> are blank, licensing is dormant — nothing is
    /// checked and nothing is blocked, so a development or pre-licensing install runs untouched.</para></summary>
    public const string EmbeddedPublicKey = "";

    /// <summary>The key actually used to verify, or null when licensing is not configured at all.</summary>
    public string? EffectivePublicKey =>
        !string.IsNullOrWhiteSpace(PublicKey) ? PublicKey.Trim()
        : !string.IsNullOrWhiteSpace(EmbeddedPublicKey) ? EmbeddedPublicKey
        : null;

    /// <summary>False until a signing key is configured — the switch that turns licensing on.</summary>
    public bool IsConfigured => EffectivePublicKey is not null;

    /// <summary>What an unlicensed instance may still do. See <see cref="LicenseEnforcementMode"/>.</summary>
    public LicenseEnforcementMode Enforcement { get; set; } = LicenseEnforcementMode.ReadOnly;

    /// <summary>How long a verified result is trusted before the file is re-checked.</summary>
    public TimeSpan RecheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public enum LicenseEnforcementMode
{
    /// <summary>Report the licence state on every response, block nothing. For pilots.</summary>
    WarnOnly = 0,
    /// <summary>Reads keep working, writes are refused. Nobody ever loses access to their archive
    /// because a licence lapsed — this is the default for that reason.</summary>
    ReadOnly = 1,
    /// <summary>Everything except signing in and installing a licence is refused.</summary>
    Block = 2,
}
