using System.Text.Json;
using Archiving.Application.Features.Letterhead;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// One-shot, idempotent migration of the old free-form correspondence designs onto slots.
///
/// The rule throughout: <b>never destroy an admin's work</b>. Every row keeps its original
/// design in <see cref="ReportTemplate.LegacyLayoutJson"/>, and anything this mapper cannot
/// place is flagged <see cref="LetterheadStatus.NeedsReview"/> with a reason rather than
/// silently dropped or approximated. A wrong letterhead on external correspondence is worse
/// than an obviously missing one.
/// </summary>
public sealed class LetterheadMigrator(AppDbContext db, ILogger<LetterheadMigrator> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        // Idempotent: a row that already carries slots or has been flagged is left alone, so this
        // is safe to run on every startup.
        var rows = await db.ReportTemplates
            .Where(t => t.Target == ReportTemplateTarget.Correspondence
                        && t.SlotsJson == null
                        && t.Status != LetterheadStatus.NeedsReview)
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;

        var palette = OrgSlots.Default.ApprovedPalette.ToList();

        foreach (var row in rows)
        {
            row.LegacyLayoutJson ??= row.LayoutJson;

            var outcome = Map(row, palette);
            if (outcome.Slots is null)
            {
                row.Status = LetterheadStatus.NeedsReview;
                row.ReviewNote = outcome.Reason;
                continue;
            }

            row.SlotsJson = outcome.Slots;
            // Migrated department designs are Active, not Pending: they are what was already on
            // paper yesterday, so holding them for approval would change live output, not protect
            // it. Only *new* edits go through the approval route.
            row.Status = LetterheadStatus.Active;
            row.ReviewNote = outcome.Reason;   // non-null when something was dropped but the rest mapped
        }

        await db.SaveChangesAsync(ct);

        var flagged = rows.Count(r => r.Status == LetterheadStatus.NeedsReview);
        log.LogInformation(
            "Letterhead migration: {Total} templates processed, {Mapped} mapped onto slots, {Flagged} flagged for review",
            rows.Count, rows.Count - flagged, flagged);
        return rows.Count;
    }

    private sealed record Outcome(string? Slots, string? Reason);

    private static Outcome Map(ReportTemplate row, List<string> palette)
    {
        var layout = row.LegacyLayoutJson ?? row.LayoutJson;
        if (string.IsNullOrWhiteSpace(layout) || layout.Trim() is "[]" or "{}")
            return Empty(row);

        JsonElement parsed;
        try { parsed = JsonDocument.Parse(layout).RootElement; }
        catch (JsonException)
        {
            return new Outcome(null, "تعذّر قراءة التصميم القديم — يحتاج إعادة إنشاء يدويًا");
        }

        // The rich designer stored a whole HTML canvas. There is no honest way to infer slots
        // from free-form markup, so it always goes to manual review.
        if (parsed.ValueKind == JsonValueKind.Object)
        {
            var kind = parsed.TryGetProperty("kind", out var k) ? k.GetString() : null;
            return new Outcome(null, kind == "rich"
                ? "تصميم حر (محرّر المحتوى) — لا يمكن تحويله تلقائيًا إلى حقول، يُرجى إعادة إنشائه"
                : "صيغة تصميم غير معروفة — يُرجى المراجعة");
        }

        if (parsed.ValueKind != JsonValueKind.Array) return Empty(row);

        var blocks = parsed.EnumerateArray().ToList();
        if (blocks.Count == 0) return Empty(row);

        string? logo = null, headerText = null, footerText = null, colour = null;
        var unmapped = new List<string>();

        foreach (var b in blocks)
        {
            var type = b.TryGetProperty("type", out var t) ? t.GetString() : null;
            switch (type)
            {
                case "logo":
                    logo = row.LogoBase64;   // the block only carried alignment/width; the image lives on the row
                    break;
                case "header":
                    headerText ??= Text(b);
                    colour ??= Str(b, "color");
                    break;
                case "footer":
                    footerText ??= Text(b);
                    break;
                case "divider" or "spacer":
                    break;                    // pure spacing — the master layout owns it now
                case "text" or "fields" or "table":
                    // These carried real content that the fixed layout now generates itself.
                    // Worth telling the admin so they can check nothing was lost.
                    unmapped.Add(Label(type));
                    break;
                default:
                    unmapped.Add(type ?? "غير معروف");
                    break;
            }
        }

        object slots = row.Scope switch
        {
            ReportTemplateScope.Default => OrgSlots.Default with
            {
                LogoBase64 = logo ?? row.LogoBase64,
                LegalFooterAr = footerText,
                ApprovedPalette = Palette(palette, colour),
                BrandAccentHex = InPalette(palette, colour) ? colour! : OrgSlots.Default.BrandAccentHex,
            },
            ReportTemplateScope.OrgUnit => new DepartmentSlots(
                DepartmentLine: Trim(headerText, 120),
                AccentHex: InPalette(palette, colour) ? colour : null),
            _ => new SignerSlots(
                SignerName: Trim(headerText, 120),
                SignerTitle: null,
                SignerContact: null),
        };

        var reason = BuildReason(unmapped, colour, palette);
        return new Outcome(JsonSerializer.Serialize(slots, Json), reason);
    }

    /// <summary>An empty design is not a problem — it just inherits. Give it default slots.</summary>
    private static Outcome Empty(ReportTemplate row)
    {
        object slots = row.Scope switch
        {
            ReportTemplateScope.Default => OrgSlots.Default with { LogoBase64 = row.LogoBase64 },
            ReportTemplateScope.OrgUnit => new DepartmentSlots(null, null),
            _ => new SignerSlots(null, null, null),
        };
        return new Outcome(JsonSerializer.Serialize(slots, Json), null);
    }

    private static string? BuildReason(List<string> unmapped, string? colour, List<string> palette)
    {
        var notes = new List<string>();
        if (unmapped.Count > 0)
            notes.Add($"عناصر لم تعد قابلة للتخصيص وتولّدها الترويسة الموحّدة الآن: {string.Join('،', unmapped.Distinct())}");
        if (colour is not null && !InPalette(palette, colour))
            notes.Add($"اللون {colour} خارج اللوحة المعتمدة ولم يُطبَّق");
        return notes.Count == 0 ? null : string.Join(" · ", notes);
    }

    private static IReadOnlyList<string> Palette(List<string> palette, string? colour) =>
        colour is not null && IsHex(colour) && !palette.Contains(colour, StringComparer.OrdinalIgnoreCase)
            ? [.. palette, colour]      // an institution's own brand colour is worth keeping approved
            : palette;

    private static bool InPalette(List<string> palette, string? colour) =>
        colour is not null && palette.Contains(colour, StringComparer.OrdinalIgnoreCase);

    private static string Label(string type) => type switch
    {
        "text" => "نص حر", "fields" => "حقول", "table" => "جدول", _ => type,
    };

    private static string? Text(JsonElement b) => Trim(Str(b, "text"), 400);
    private static string? Str(JsonElement b, string name) =>
        b.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Trim(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Length <= max ? s.Trim() : s[..max].Trim());

    private static bool IsHex(string v) =>
        System.Text.RegularExpressions.Regex.IsMatch(v, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
}
