using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Archiving.Domain.Entities;

namespace Archiving.Infrastructure.Services;

/// <summary>The canonical hash function for the tamper-evident audit chain. Each entry's hash
/// covers the previous entry's hash plus the entry's content, so any later modification breaks
/// the chain. Shared by the writer (to seal entries) and the verifier (to re-check them).
/// The timestamp is formatted to microsecond precision with no kind marker so the hash is stable
/// across persistence (MySQL <c>datetime(6)</c>) — see <see cref="Truncate"/>.</summary>
public static class AuditHash
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss.ffffff";

    public static string Compute(AuditLog e, string? previousHash)
    {
        var payload = string.Join('|',
            previousHash, e.UserId, e.Action, e.EntityType, e.EntityId,
            e.OldValues, e.NewValues, e.IpAddress, e.CreatedAt.ToString(TimeFormat, CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Truncates a timestamp to whole microseconds so the value stored in
    /// <c>datetime(6)</c> equals the value that was hashed (no rounding drift on round-trip).</summary>
    public static DateTime Truncate(DateTime t) =>
        new(t.Ticks - (t.Ticks % TimeSpan.TicksPerMicrosecond), DateTimeKind.Utc);
}
