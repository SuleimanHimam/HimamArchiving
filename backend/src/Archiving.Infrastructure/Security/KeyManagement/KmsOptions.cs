namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>Configuration for the key-management layer (<c>Kms</c> section).</summary>
public sealed class KmsOptions
{
    public const string SectionName = "Kms";

    /// <summary><c>DpapiKeyring</c> (Windows, machine-scoped) · <c>ExternalKms</c> (HashiCorp Vault Transit,
    /// Azure Key Vault or AWS KMS behind the adapter) · <c>Insecure</c> (a raw key in config — refused
    /// outside Development).</summary>
    public string Provider { get; set; } = "DpapiKeyring";

    /// <summary>Keyring file for <c>DpapiKeyring</c>. Must sit outside the object store and outside the
    /// web root, on a volume with its own ACL. Back it up separately from the archive: whoever holds both
    /// the keyring and the object store holds the plaintext.</summary>
    public string KeyringPath { get; set; } = @"C:\ArchivingData\keys\keyring.dat";

    /// <summary>Base address of the external KMS for <c>ExternalKms</c>, e.g.
    /// <c>https://vault.internal:8200/v1/transit</c>. HTTPS is required.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Named key inside the external KMS that acts as the KEK.</summary>
    public string? KeyName { get; set; }

    /// <summary>Bearer token / access key for the external KMS. Prefer an injected environment variable
    /// or a managed identity over a value written into appsettings.</summary>
    public string? Token { get; set; }

    /// <summary>Dev-only raw 32-byte base64 KEK for <c>Insecure</c>. Startup refuses it in Production.</summary>
    public string? InsecureKey { get; set; }

    /// <summary>Days before the active KEK is considered due for rotation. 0 disables the scheduled sweep;
    /// rotation can always be run on demand.</summary>
    public int RotationIntervalDays { get; set; } = 90;

    /// <summary>Rows re-wrapped per batch by the rotation sweep. Keeps the transaction and the KMS call
    /// rate bounded on an archive with millions of objects.</summary>
    public int RewrapBatchSize { get; set; } = 500;
}
