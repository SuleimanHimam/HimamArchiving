using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Letterhead;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// Resolves the three letterhead levels and hands them to the master layout.
///
/// Authorization lives in the controller; what lives here is the rule that cannot be bypassed by
/// any caller: a department's slots only take effect once they are Active, and a department
/// accent must be one the institution approved.
/// </summary>
public sealed class LetterheadService(
    AppDbContext db,
    ILetterheadRenderer renderer,
    IWorkflowService workflow,
    ICurrentUser currentUser) : ILetterheadService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string WorkflowEntityType = "Letterhead";
    public const string WorkflowTriggerModule = "Letterhead";

    // ── Resolution ───────────────────────────────────────────────────────────────

    public async Task<ResolvedLetterhead> ResolveAsync(long? userId, long? orgUnitId, CancellationToken ct = default)
    {
        var org = await GetOrgSlotsAsync(ct);

        var dept = orgUnitId is { } unit ? await GetActiveDepartmentSlotsAsync(unit, ct) : new DepartmentSlots(null, null);
        // A department accent that is no longer in the approved palette is dropped rather than
        // honoured: the palette may have been tightened after the department was approved, and the
        // institution's baseline must win.
        if (dept.AccentHex is { } accent && !org.ApprovedPalette.Contains(accent, StringComparer.OrdinalIgnoreCase))
            dept = dept with { AccentHex = null };

        var signer = userId is { } uid ? await GetSignerSlotsAsync(uid, ct) : new SignerSlots(null, null, null);
        return new ResolvedLetterhead(org, dept, signer);
    }

    public async Task<OrgSlots> GetOrgSlotsAsync(CancellationToken ct = default)
    {
        var row = await Query(ReportTemplateScope.Default).FirstOrDefaultAsync(ct);
        return Read<OrgSlots>(row?.SlotsJson) ?? OrgSlots.Default;
    }

    public async Task<DepartmentSlots> GetDepartmentSlotsAsync(long orgUnitId, CancellationToken ct = default)
    {
        var row = await Query(ReportTemplateScope.OrgUnit).FirstOrDefaultAsync(t => t.OrgUnitId == orgUnitId, ct);
        return Read<DepartmentSlots>(row?.SlotsJson) ?? new DepartmentSlots(null, null);
    }

    /// <summary>What actually prints — a pending or rejected edit is invisible to the renderer.</summary>
    private async Task<DepartmentSlots> GetActiveDepartmentSlotsAsync(long orgUnitId, CancellationToken ct)
    {
        var row = await Query(ReportTemplateScope.OrgUnit)
            .FirstOrDefaultAsync(t => t.OrgUnitId == orgUnitId && t.Status == LetterheadStatus.Active, ct);
        return Read<DepartmentSlots>(row?.SlotsJson) ?? new DepartmentSlots(null, null);
    }

    public async Task<SignerSlots> GetSignerSlotsAsync(long userId, CancellationToken ct = default)
    {
        var row = await Query(ReportTemplateScope.User).FirstOrDefaultAsync(t => t.UserId == userId, ct);
        var stored = Read<SignerSlots>(row?.SlotsJson);
        if (stored is not null) return stored;

        // An employee who never opened the designer still signs their own letters: fall back to
        // their directory record rather than printing an unsigned letter.
        var user = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.JobTitle, u.Email }).FirstOrDefaultAsync(ct);
        return user is null
            ? new SignerSlots(null, null, null)
            : new SignerSlots(user.FullName, user.JobTitle, user.Email);
    }

    // ── Writes ───────────────────────────────────────────────────────────────────

    public async Task<Result<OrgSlots>> SaveOrgSlotsAsync(OrgSlots slots, CancellationToken ct = default)
    {
        if (Validate(slots) is { } error) return Result<OrgSlots>.Fail(error);

        var row = await Query(ReportTemplateScope.Default).FirstOrDefaultAsync(ct)
                  ?? await NewRowAsync(ReportTemplateScope.Default, "ترويسة المؤسسة", ct);

        row.SlotsJson = JsonSerializer.Serialize(slots, Json);
        // Org slots are the locked baseline — there is no level above them to approve.
        row.Status = LetterheadStatus.Active;
        row.ReviewNote = null;
        await db.SaveChangesAsync(ct);
        return Result<OrgSlots>.Ok(slots);
    }

    public async Task<Result<DepartmentSlots>> SaveDepartmentSlotsAsync(
        long orgUnitId, DepartmentSlots slots, CancellationToken ct = default)
    {
        var org = await GetOrgSlotsAsync(ct);
        if (slots.AccentHex is { } accent
            && !org.ApprovedPalette.Contains(accent, StringComparer.OrdinalIgnoreCase))
            return Result<DepartmentSlots>.Fail("اللون المختار ليس ضمن ألوان المؤسسة المعتمدة");

        if (slots.DepartmentLine?.Length > 120)
            return Result<DepartmentSlots>.Fail("سطر القسم يتجاوز 120 حرفًا");

        var row = await Query(ReportTemplateScope.OrgUnit).FirstOrDefaultAsync(t => t.OrgUnitId == orgUnitId, ct);
        if (row is null)
        {
            row = await NewRowAsync(ReportTemplateScope.OrgUnit, "ترويسة القسم", ct);
            row.OrgUnitId = orgUnitId;
        }

        row.SlotsJson = JsonSerializer.Serialize(slots, Json);
        // The edit is parked, not applied: nothing on paper changes until approval completes.
        row.Status = LetterheadStatus.PendingApproval;
        await db.SaveChangesAsync(ct);

        var definition = await db.WorkflowDefinitions
            .Where(d => d.TriggerModule == WorkflowTriggerModule && d.IsActive)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(ct);

        if (definition is null)
        {
            // No approval route configured. Refuse rather than silently publishing an unreviewed
            // change — requirement 4 exists precisely to stop that.
            return Result<DepartmentSlots>.Fail(
                "لا يوجد مسار اعتماد لترويسات الأقسام — يُرجى إعداده قبل تعديل ترويسة القسم");
        }

        var started = await workflow.StartAsync(new StartWorkflowRequest(definition.Id, WorkflowEntityType, row.Id), ct);
        if (!started.Succeeded) return Result<DepartmentSlots>.Fail(started.Error ?? "تعذّر بدء مسار الاعتماد");

        row.ApprovalWorkflowInstanceId = started.Value!.Id;
        await db.SaveChangesAsync(ct);
        return Result<DepartmentSlots>.Ok(slots);
    }

    public async Task<Result<SignerSlots>> SaveSignerSlotsAsync(
        long userId, SignerSlots slots, CancellationToken ct = default)
    {
        if (slots.SignerName?.Length > 120 || slots.SignerTitle?.Length > 120 || slots.SignerContact?.Length > 160)
            return Result<SignerSlots>.Fail("أحد حقول التوقيع يتجاوز الحد المسموح");

        var row = await Query(ReportTemplateScope.User).FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (row is null)
        {
            row = await NewRowAsync(ReportTemplateScope.User, "توقيع المستخدم", ct);
            row.UserId = userId;
        }

        row.SlotsJson = JsonSerializer.Serialize(slots, Json);
        // No approval: a signature block changes nothing but the signer's own name.
        row.Status = LetterheadStatus.Active;
        await db.SaveChangesAsync(ct);
        return Result<SignerSlots>.Ok(slots);
    }

    // ── Rendering ────────────────────────────────────────────────────────────────

    public async Task<Result<byte[]>> RenderDiwanLetterAsync(long diwanMailId, CancellationToken ct = default)
    {
        var mail = await db.DiwanMails.FirstOrDefaultAsync(m => m.Id == diwanMailId, ct);
        if (mail is null) return Result<byte[]>.Fail("المراسلة غير موجودة");
        if (mail.Direction != DiwanDirection.Outgoing)
            return Result<byte[]>.Fail("الترويسة تُطبع للكتب الصادرة فقط — الوارد يُطبع من صورته الممسوحة");

        // The design follows the letter's author, so a clerk printing someone else's letter
        // reproduces the letterhead that author would have got.
        var authorOrgUnitId = mail.CreatedBy is { } author
            ? await db.Users.Where(u => u.Id == author).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct)
            : null;

        var letterhead = await ResolveAsync(mail.CreatedBy, authorOrgUnitId, ct);
        var institution = await db.Institutions.Select(i => i.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

        return Result<byte[]>.Ok(renderer.Render(letterhead, ToContent(mail), institution));
    }

    public async Task<Result<byte[]>> RenderPreviewAsync(
        PreviewLetterheadRequest request, CancellationToken ct = default)
    {
        // Unsaved values win over stored ones — that is what makes the designer's preview live.
        var org = request.Org ?? await GetOrgSlotsAsync(ct);
        if (Validate(org) is { } error) return Result<byte[]>.Fail(error);

        var dept = request.Department
                   ?? (request.OrgUnitId is { } unit ? await GetDepartmentSlotsAsync(unit, ct) : new DepartmentSlots(null, null));

        var signer = request.Signer
                     ?? await GetSignerSlotsAsync(request.UserId ?? currentUser.UserId ?? 0, ct);

        var letterhead = new ResolvedLetterhead(org, dept, signer);
        var institution = await db.Institutions.Select(i => i.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

        return Result<byte[]>.Ok(renderer.Render(letterhead, request.Content ?? SampleContent, institution));
    }

    public async Task<IReadOnlyList<LetterheadReviewItem>> ListNeedsReviewAsync(CancellationToken ct = default)
        => await db.ReportTemplates
            .Where(t => t.Status == LetterheadStatus.NeedsReview)
            .OrderBy(t => t.Scope).ThenBy(t => t.Id)
            .Select(t => new LetterheadReviewItem(
                t.Id,
                t.Name,
                t.Scope.ToString(),
                t.OrgUnitId == null ? null : db.OrgUnits.Where(o => o.Id == t.OrgUnitId).Select(o => o.Name).FirstOrDefault(),
                t.UserId == null ? null : db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault(),
                t.ReviewNote,
                t.CreatedAt))
            .ToListAsync(ct);

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Correspondence letterheads only — document designs still use the block renderer.</summary>
    private IQueryable<ReportTemplate> Query(ReportTemplateScope scope) =>
        db.ReportTemplates.Where(t =>
            t.Scope == scope
            && t.Target == ReportTemplateTarget.Correspondence
            && t.MailCategoryId == null
            && t.Status != LetterheadStatus.NeedsReview);

    private async Task<ReportTemplate> NewRowAsync(ReportTemplateScope scope, string name, CancellationToken ct)
    {
        var institutionId = await db.Institutions.Select(i => i.Id).FirstOrDefaultAsync(ct);
        var row = new ReportTemplate
        {
            InstitutionId = institutionId,
            Scope = scope,
            Target = ReportTemplateTarget.Correspondence,
            Name = name,
            LayoutJson = "[]",
            Status = LetterheadStatus.Draft,
        };
        db.ReportTemplates.Add(row);
        return row;
    }

    private static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        // A row hand-edited into invalid JSON must not break every letter in the institution;
        // treat it as "not configured" and inherit from the level above.
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    private static string? Validate(OrgSlots s)
    {
        if (!IsHex(s.BrandPrimaryHex) || !IsHex(s.BrandAccentHex)) return "قيمة لون غير صالحة";
        if (s.ApprovedPalette.Count == 0) return "يجب اعتماد لون واحد على الأقل في لوحة الألوان";
        if (s.ApprovedPalette.Any(c => !IsHex(c))) return "لوحة الألوان تحتوي قيمة غير صالحة";
        if (s.BaseFontPt is < 8 or > 16) return "حجم الخط يجب أن يكون بين 8 و16";
        int[] margins = [s.MarginTopMm, s.MarginBottomMm, s.MarginInnerMm, s.MarginOuterMm];
        if (margins.Any(m => m is < 5 or > 40)) return "الهوامش يجب أن تكون بين 5 و40 مم";
        return null;
    }

    private static bool IsHex(string? v) =>
        v is not null && System.Text.RegularExpressions.Regex.IsMatch(v, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

    private static LetterContent ToContent(DiwanMail m) => new(
        RegisterNumber: m.RegisterNumber,
        Subject: m.Subject ?? string.Empty,
        MailDate: m.MailDate.ToString("yyyy-MM-dd"),
        SentDate: m.SentDate?.ToString("yyyy-MM-dd"),
        RecipientOrg: m.ExternalOrgName,
        AddresseeName: m.AddresseeName,
        AddresseeTitle: m.AddresseeTitle,
        Body: PlainText(m.Body),
        Classification: m.Classification.ToString(),
        FileNumber: m.FileNumber);

    /// <summary>
    /// Letters authored before this change hold TipTap HTML. The body zone is plain text by
    /// design, so tags are stripped and block boundaries become paragraph breaks — an old letter
    /// still prints its words, just in the house style.
    /// </summary>
    internal static string PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var withBreaks = System.Text.RegularExpressions.Regex.Replace(
            html, @"</(p|div|h[1-6]|li|tr)\s*>|<br\s*/?>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var stripped = System.Text.RegularExpressions.Regex.Replace(withBreaks, "<[^>]+>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(stripped).Trim();
    }

    /// <summary>Filler for the designer's preview, so a letterhead can be judged before any real
    /// letter exists.</summary>
    private static LetterContent SampleContent { get; } = new(
        RegisterNumber: "DIW-2026-0001",
        Subject: "نموذج لمعاينة الترويسة",
        MailDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
        SentDate: null,
        RecipientOrg: "الجهة المستلمة",
        AddresseeName: "اسم المخاطَب",
        AddresseeTitle: "المسمى الوظيفي",
        Body: "تحية طيبة وبعد،\nهذا نص تجريبي يوضّح كيف يظهر متن الكتاب على الترويسة المعتمدة.",
        Classification: null,
        FileNumber: null);
}
