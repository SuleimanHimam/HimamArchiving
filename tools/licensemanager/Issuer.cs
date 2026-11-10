using Archiving.Infrastructure.Licensing;

namespace Archiving.LicenseManager;

/// <summary>What the operator filled in on the issue form, before it becomes a signed licence.</summary>
internal sealed record IssueRequest
{
    public string Customer { get; init; } = "";
    public string MachineId { get; init; } = "";
    public string Edition { get; init; } = "Standard";
    /// <summary>Null for a perpetual licence.</summary>
    public DateTime? ExpiresAt { get; init; }
    public int GraceDays { get; init; } = 14;
    public int? MaxUsers { get; init; }
    public string[] Features { get; init; } = [];
    public string? Notes { get; init; }
    /// <summary>Set when this replaces an earlier licence, so the ledger keeps the chain.</summary>
    public string? Supersedes { get; init; }
}

/// <summary>
/// Signs a licence and records it, in that order and never one without the other.
///
/// Keeping the two together is the whole point of this class. The CLI could sign a licence and leave
/// no trace of it; a licence that exists in a customer's hands but not in the vendor's records is
/// the thing that makes support impossible two years later, when nobody remembers what was sold.
/// </summary>
internal static class Issuer
{
    public static LedgerEntry Issue(IssueRequest request, Vault vault, Ledger ledger)
    {
        if (string.IsNullOrWhiteSpace(request.Customer))
            throw new InvalidOperationException("اسم العميل مطلوب — وهو يظهر داخل الترخيص ويقرأه العميل.");

        var check = Fingerprint.Check(request.MachineId);
        if (!check.Usable)
            throw new InvalidOperationException(check.Message ?? "بصمة الجهاز غير صالحة.");

        var payload = new LicensePayload
        {
            Id = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            Customer = request.Customer.Trim(),
            MachineId = check.Value,
            Edition = string.IsNullOrWhiteSpace(request.Edition) ? "Standard" : request.Edition.Trim(),
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            GraceDays = Math.Max(0, request.GraceDays),
            MaxUsers = request.MaxUsers,
            Features = request.Features,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        string token;
        using (var key = vault.OpenKey())
            token = LicenseToken.Create(payload, key);

        // Verified against our own public key before it is recorded or handed over. Signing and
        // verifying are different code paths, and the only bad moment to discover they disagree is
        // after the customer has the file.
        using (var check2 = vault.OpenKey())
        {
            if (!LicenseToken.TryRead(token, check2, out _, out var error))
                throw new InvalidOperationException(
                    $"وُقّع الترخيص لكنه لم يجتز التحقّق: {error}\n\nلم يُسجَّل ولم يُحفَظ.");
        }

        var entry = new LedgerEntry
        {
            Id = payload.Id,
            Customer = payload.Customer,
            MachineId = payload.MachineId,
            Edition = payload.Edition,
            IssuedAt = payload.IssuedAt,
            ExpiresAt = payload.ExpiresAt,
            GraceDays = payload.GraceDays,
            MaxUsers = payload.MaxUsers,
            Features = payload.Features,
            Notes = payload.Notes,
            Token = token,
            Supersedes = request.Supersedes,
        };

        ledger.Add(entry);
        return entry;
    }

    /// <summary>
    /// Issues a fresh licence carrying an existing one's terms, and marks the old one superseded.
    ///
    /// A renewal is a new signature, not an edited licence — the old token stays valid until it
    /// expires on its own, which is what lets a customer keep running while the new file is still in
    /// somebody's inbox.
    /// </summary>
    public static LedgerEntry Renew(LedgerEntry previous, DateTime? newExpiry, Vault vault, Ledger ledger)
    {
        var entry = Issue(new IssueRequest
        {
            Customer = previous.Customer,
            MachineId = previous.MachineId,
            Edition = previous.Edition,
            ExpiresAt = newExpiry,
            GraceDays = previous.GraceDays,
            MaxUsers = previous.MaxUsers,
            Features = previous.Features,
            Notes = previous.Notes,
            Supersedes = previous.Id,
        }, vault, ledger);

        ledger.Replace(previous with
        {
            RevokedAt = DateTime.UtcNow,
            RevokedReason = $"جُدِّد بالترخيص {entry.Id}",
        });

        return entry;
    }

    /// <summary>Reads a token or a .lic file and says what it contains and whether it verifies.
    /// Used for the licence a customer mails back asking why it will not work.</summary>
    public static string Inspect(string tokenOrPath, Vault vault)
    {
        var token = File.Exists(tokenOrPath) ? File.ReadAllText(tokenOrPath).Trim() : tokenOrPath.Trim();
        if (token.Length == 0) return "لا يوجد شيء لفحصه.";

        var peek = LicenseToken.PeekUnverified(token);
        if (peek is null)
            return "هذا النص ليس ترخيصًا بصيغة هذا النظام (المتوقّع: ARCHLIC-1.<payload>.<signature>).";

        var lines = new List<string>
        {
            $"المعرّف       : {peek.Id}",
            $"العميل        : {peek.Customer}",
            $"الجهاز        : {peek.MachineId}",
            $"الإصدار       : {peek.Edition}",
            $"صدر في        : {peek.IssuedAt:yyyy-MM-dd HH:mm} UTC",
            $"ينتهي في      : {(peek.ExpiresAt is null ? "دائم" : peek.ExpiresAt.Value.ToString("yyyy-MM-dd"))}",
            $"مهلة السماح   : {peek.GraceDays} يومًا",
            $"حد المستخدمين : {(peek.MaxUsers is null ? "غير محدود" : peek.MaxUsers.ToString())}",
            $"الوحدات       : {(peek.Features.Length == 0 ? "الكل" : string.Join(", ", peek.Features))}",
        };
        if (!string.IsNullOrWhiteSpace(peek.Notes)) lines.Add($"ملاحظات       : {peek.Notes}");

        lines.Add("");
        if (!vault.KeyAvailable)
        {
            // Said plainly rather than implied: the fields above are read from an unverified payload,
            // and without the key this program cannot tell a real licence from a forged one.
            lines.Add("التوقيع       : لم يُفحَص — لا يوجد مفتاح محمَّل. البيانات أعلاه غير موثَّقة.");
            return string.Join(Environment.NewLine, lines);
        }

        using var key = vault.OpenKey();
        lines.Add(LicenseToken.TryRead(token, key, out _, out var error)
            ? "التوقيع       : صالح ✓"
            : $"التوقيع       : غير صالح ✗ — {error}");

        // The verifier checks the machine binding against the machine it runs on, which here is the
        // vendor's desk, not the customer's server. Saying so avoids a confusing "wrong machine".
        lines.Add("");
        lines.Add("ملاحظة: يتحقّق هذا الفحص من التوقيع ومحتوى الترخيص فقط. ارتباط الترخيص بالجهاز");
        lines.Add("يُفحص على خادم العميل نفسه، لا هنا.");

        return string.Join(Environment.NewLine, lines);
    }
}
