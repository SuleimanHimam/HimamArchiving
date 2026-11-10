using System.Text.Json;
using System.Text.Json.Serialization;

namespace Archiving.LicenseManager;

/// <summary>One licence, as it was issued. The record the CLI never kept.</summary>
internal sealed record LedgerEntry
{
    [JsonPropertyName("id")]         public string Id { get; init; } = "";
    [JsonPropertyName("customer")]   public string Customer { get; init; } = "";
    [JsonPropertyName("machineId")]  public string MachineId { get; init; } = "";
    [JsonPropertyName("edition")]    public string Edition { get; init; } = "Standard";
    [JsonPropertyName("issuedAt")]   public DateTime IssuedAt { get; init; }
    [JsonPropertyName("expiresAt")]  public DateTime? ExpiresAt { get; init; }
    [JsonPropertyName("graceDays")]  public int GraceDays { get; init; }
    [JsonPropertyName("maxUsers")]   public int? MaxUsers { get; init; }
    [JsonPropertyName("features")]   public string[] Features { get; init; } = [];
    [JsonPropertyName("notes")]      public string? Notes { get; init; }

    /// <summary>The signed token itself. Stored so a licence can be re-sent to a customer who lost
    /// it without minting a second one — two live licences for one machine is a support problem
    /// that starts quietly and is very hard to unpick later.</summary>
    [JsonPropertyName("token")]      public string Token { get; init; } = "";

    /// <summary>Set when a licence is deliberately withdrawn. Nothing enforces this at the customer
    /// (there is no call home, by design) — it is a vendor-side note so nobody re-sends a licence
    /// that was replaced or refunded.</summary>
    [JsonPropertyName("revokedAt")]  public DateTime? RevokedAt { get; init; }
    [JsonPropertyName("revokedReason")] public string? RevokedReason { get; init; }

    /// <summary>The id this one replaces, when it was produced by Renew.</summary>
    [JsonPropertyName("supersedes")] public string? Supersedes { get; init; }

    // ── derived state, for the list ──────────────────────────────────────────

    [JsonIgnore] public bool IsPerpetual => ExpiresAt is null;

    [JsonIgnore]
    public int? DaysLeft => ExpiresAt is null
        ? null
        : (int)Math.Floor((ExpiresAt.Value - DateTime.UtcNow).TotalDays);

    [JsonIgnore]
    public LicenceState State
    {
        get
        {
            if (RevokedAt is not null) return LicenceState.Revoked;
            if (ExpiresAt is null) return LicenceState.Perpetual;
            var left = DaysLeft!.Value;
            if (left < -GraceDays) return LicenceState.Expired;
            if (left < 0) return LicenceState.InGrace;
            if (left <= 30) return LicenceState.Expiring;
            return LicenceState.Active;
        }
    }
}

internal enum LicenceState { Active, Expiring, InGrace, Expired, Perpetual, Revoked }

/// <summary>
/// The record of every licence ever issued.
///
/// The CLI could mint a licence and forget it the same second, which left the vendor unable to
/// answer the three questions that actually come up: what did we sell this customer, whose licence
/// expires next month, and have we already issued one for this machine? A flat JSON file answers
/// all three, is readable without this program, and survives it.
///
/// Deliberately not a database. This file lives beside the signing key, gets backed up with it, and
/// a vendor with a few hundred customers will never notice the difference.
/// </summary>
internal sealed class Ledger
{
    private readonly string _path;
    private readonly List<LedgerEntry> _entries;

    private Ledger(string path, List<LedgerEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    public IReadOnlyList<LedgerEntry> Entries => _entries;
    public string Path => _path;

    public static Ledger Load(string path)
    {
        if (!File.Exists(path)) return new Ledger(path, []);
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<LedgerEntry>>(json) ?? [];
            return new Ledger(path, list);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Never start from an empty list after a failed read — that would quietly overwrite the
            // whole history on the next issue. Refusing loudly is the only safe behaviour here.
            throw new InvalidOperationException(
                $"تعذّرت قراءة سجل التراخيص {path}: {ex.Message}\n\n" +
                "لن يبدأ البرنامج بسجل فارغ حتى لا يُتلف السجل الحالي. أصلح الملف أو انقله جانبًا.", ex);
        }
    }

    public void Add(LedgerEntry entry)
    {
        _entries.Add(entry);
        Save();
    }

    public void Replace(LedgerEntry updated)
    {
        var i = _entries.FindIndex(e => e.Id == updated.Id);
        if (i < 0) throw new InvalidOperationException($"لا يوجد ترخيص بالمعرّف {updated.Id}.");
        _entries[i] = updated;
        Save();
    }

    /// <summary>Live licences already bound to this machine. Surfaced before issuing so the operator
    /// makes a deliberate choice — renewing an existing licence rather than adding a second one that
    /// will confuse whoever handles the next support call.</summary>
    public IReadOnlyList<LedgerEntry> LiveFor(string machineId) =>
        _entries.Where(e => e.MachineId.Equals(machineId, StringComparison.OrdinalIgnoreCase)
                         && e.State is not (LicenceState.Revoked or LicenceState.Expired))
                .ToList();

    private void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Written to a temporary file and moved into place: a crash halfway through a write would
        // otherwise leave a truncated ledger, and this file is the only record that a licence exists.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Options));
        File.Move(tmp, _path, overwrite: true);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
