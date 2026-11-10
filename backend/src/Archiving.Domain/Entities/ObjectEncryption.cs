using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>
/// The metadata index row for one encrypted object on the file server: where its bytes are, which KEK
/// version sealed its DEK, and the wrapped DEK itself.
///
/// <para><b>The wrapped DEK lives here, in the database — not in the file header.</b> That one choice is
/// what makes the two hardest requirements cheap:</para>
/// <list type="bullet">
///   <item><description><b>Crypto-shredding</b> is a single transactional column write, not an in-place
///   overwrite of a file. Overwriting bytes on an SSD or a copy-on-write volume does not reliably destroy
///   them (wear levelling, journals, shadow copies); nulling a database column does.</description></item>
///   <item><description><b>Key rotation</b> re-wraps rows in this table. The object store — which may be
///   terabytes across a NAS — is never rewritten.</description></item>
/// </list>
///
/// <para>Corollary the operator must own: a database backup taken before a shred still contains the
/// wrapped DEK. See <c>ShreddedAtUtc</c> and the restore procedure in the security checklist.</para>
/// </summary>
public class ArchivedObject : BaseEntity
{
    /// <summary>Logical key handed to <c>IFileStorage</c>, e.g. <c>documents/2026/09/3f/a1/{uuid}.enc</c>.
    /// Unique; also the relative path under the storage root.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Plaintext byte count. The file on disk is larger by the header and per-frame tags.</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the <b>plaintext</b>, so fixity, OCR and preservation compare like with like
    /// and survive a re-encryption under a new key.</summary>
    public string ChecksumSha256 { get; set; } = string.Empty;

    /// <summary>Cipher suite that sealed the object. Recorded rather than assumed, so a future suite can
    /// be introduced without guessing at existing objects.</summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>KEK version that wrapped <see cref="WrappedDek"/>. Rotation selects on this.</summary>
    public string KekId { get; set; } = string.Empty;

    /// <summary>The DEK sealed by the KEK. Null once the object has been crypto-shredded — at which point
    /// the ciphertext on disk is permanently undecryptable, by anyone, including us.</summary>
    public byte[]? WrappedDek { get; set; }

    /// <summary>Per-object nonce prefix, mirrored from the container header. Not a secret: it is indexed
    /// here so an operator can prove nonce uniqueness across the archive without reading every file.</summary>
    public byte[]? NoncePrefix { get; set; }

    /// <summary>Plaintext bytes per AEAD frame, as sealed into the header.</summary>
    public int ChunkSize { get; set; }

    /// <summary>Set when the DEK was destroyed. The row survives as proof the object once existed and was
    /// deliberately destroyed — ISO 16363 wants the evidence, not silence.</summary>
    public DateTime? ShreddedAtUtc { get; set; }
    public long? ShreddedByUserId { get; set; }

    /// <summary>When the DEK was last re-wrapped under a newer KEK. Drives the rotation report.</summary>
    public DateTime? LastRewrappedAtUtc { get; set; }

    public bool IsShredded => ShreddedAtUtc is not null;
}
