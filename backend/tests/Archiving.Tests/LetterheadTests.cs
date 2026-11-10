using System.Text;
using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Letterhead;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using Xunit;

namespace Archiving.Tests;

/// <summary>
/// The slot-based letterhead. What is worth asserting here is not that the code runs but that the
/// three rules the module exists to enforce actually hold: an unapproved department edit never
/// reaches paper, a department cannot pick a colour the institution did not approve, and a legacy
/// design is never silently discarded.
/// </summary>
public class LetterheadTests
{
    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Institutions.Add(new Institution { Name = "مؤسسة الاختبار" });
        db.SaveChanges();
        return db;
    }

    private static LetterheadService Svc(AppDbContext db, IWorkflowService? wf = null) =>
        new(db, new LetterheadRenderer(), wf ?? new NoopWorkflowService(), new TestCurrentUser());

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ReportTemplate Row(ReportTemplateScope scope, object slots, LetterheadStatus status,
        long? orgUnitId = null, long? userId = null) => new()
        {
            InstitutionId = 1,
            Scope = scope,
            Target = ReportTemplateTarget.Correspondence,
            OrgUnitId = orgUnitId,
            UserId = userId,
            Name = "t",
            SlotsJson = JsonSerializer.Serialize(slots, Json),
            Status = status,
        };

    // ── Resolution ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_department_slots_never_reach_the_rendered_letter()
    {
        using var db = NewDb();
        db.ReportTemplates.Add(Row(ReportTemplateScope.OrgUnit,
            new DepartmentSlots("قسم لم يُعتمد بعد", "#9B2226"), LetterheadStatus.PendingApproval, orgUnitId: 7));
        await db.SaveChangesAsync();

        var resolved = await Svc(db).ResolveAsync(userId: null, orgUnitId: 7);

        Assert.Null(resolved.Department.DepartmentLine);
        // Falls back to the institution accent rather than the unapproved one.
        Assert.Equal(OrgSlots.Default.BrandAccentHex, resolved.EffectiveAccentHex);
    }

    [Fact]
    public async Task Active_department_slots_override_the_org_accent()
    {
        using var db = NewDb();
        var accent = OrgSlots.Default.ApprovedPalette[1];
        db.ReportTemplates.Add(Row(ReportTemplateScope.OrgUnit,
            new DepartmentSlots("دائرة الشؤون المالية", accent), LetterheadStatus.Active, orgUnitId: 7));
        await db.SaveChangesAsync();

        var resolved = await Svc(db).ResolveAsync(userId: null, orgUnitId: 7);

        Assert.Equal("دائرة الشؤون المالية", resolved.Department.DepartmentLine);
        Assert.Equal(accent, resolved.EffectiveAccentHex);
    }

    [Fact]
    public async Task Department_accent_dropped_when_the_palette_no_longer_approves_it()
    {
        using var db = NewDb();
        // Approved when it was saved; the institution has since tightened the palette.
        db.ReportTemplates.Add(Row(ReportTemplateScope.Default,
            OrgSlots.Default with { ApprovedPalette = ["#14213D"] }, LetterheadStatus.Active));
        db.ReportTemplates.Add(Row(ReportTemplateScope.OrgUnit,
            new DepartmentSlots("قسم", "#9B2226"), LetterheadStatus.Active, orgUnitId: 7));
        await db.SaveChangesAsync();

        var resolved = await Svc(db).ResolveAsync(userId: null, orgUnitId: 7);

        Assert.Null(resolved.Department.AccentHex);
        Assert.Equal(OrgSlots.Default.BrandAccentHex, resolved.EffectiveAccentHex);
    }

    // ── Validation ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Department_cannot_pick_a_colour_outside_the_approved_palette()
    {
        using var db = NewDb();

        var result = await Svc(db).SaveDepartmentSlotsAsync(7, new DepartmentSlots("قسم", "#FF00FF"));

        Assert.False(result.Succeeded);
        Assert.Contains("المعتمدة", result.Error);
        Assert.Empty(await db.ReportTemplates.Where(t => t.Scope == ReportTemplateScope.OrgUnit).ToListAsync());
    }

    [Fact]
    public async Task Department_edit_is_parked_pending_and_refused_without_an_approval_route()
    {
        using var db = NewDb();
        // No workflow definition seeded: publishing unreviewed would defeat the whole requirement.
        var result = await Svc(db).SaveDepartmentSlotsAsync(7,
            new DepartmentSlots("قسم", OrgSlots.Default.ApprovedPalette[0]));

        Assert.False(result.Succeeded);
        var row = await db.ReportTemplates.SingleAsync(t => t.Scope == ReportTemplateScope.OrgUnit);
        Assert.Equal(LetterheadStatus.PendingApproval, row.Status);
    }

    [Theory]
    [InlineData("#GGGGGG")]
    [InlineData("not-a-colour")]
    public async Task Org_slots_reject_a_malformed_colour(string bad)
    {
        using var db = NewDb();
        var result = await Svc(db).SaveOrgSlotsAsync(OrgSlots.Default with { BrandPrimaryHex = bad });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Org_slots_reject_margins_outside_the_printable_range()
    {
        using var db = NewDb();
        var result = await Svc(db).SaveOrgSlotsAsync(OrgSlots.Default with { MarginTopMm = 90 });
        Assert.False(result.Succeeded);
    }

    // ── Signer fallback ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Signer_falls_back_to_the_directory_when_never_configured()
    {
        using var db = NewDb();
        db.Users.Add(new User { Id = 42, FullName = "سليمان", JobTitle = "مدير الدائرة", Email = "s@x.gov" });
        await db.SaveChangesAsync();

        var signer = await Svc(db).GetSignerSlotsAsync(42);

        Assert.Equal("سليمان", signer.SignerName);
        Assert.Equal("مدير الدائرة", signer.SignerTitle);
    }

    // ── Legacy migration ─────────────────────────────────────────────────────────

    private static LetterheadMigrator Migrator(AppDbContext db) =>
        new(db, NullLogger<LetterheadMigrator>.Instance);

    [Fact]
    public async Task Free_form_rich_design_is_flagged_for_review_and_never_deleted()
    {
        using var db = NewDb();
        const string rich = """{"kind":"rich","html":"<p>ترويسة مرسومة يدويًا</p>"}""";
        db.ReportTemplates.Add(new ReportTemplate
        {
            InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit,
            Target = ReportTemplateTarget.Correspondence, Name = "قديم", LayoutJson = rich,
        });
        await db.SaveChangesAsync();

        await Migrator(db).RunAsync();

        var row = await db.ReportTemplates.SingleAsync();
        Assert.Equal(LetterheadStatus.NeedsReview, row.Status);
        Assert.Null(row.SlotsJson);                     // nothing was invented
        Assert.Equal(rich, row.LegacyLayoutJson);       // the original survives verbatim
        Assert.Contains("تصميم حر", row.ReviewNote);
    }

    [Fact]
    public async Task Block_design_maps_header_onto_the_department_line()
    {
        using var db = NewDb();
        db.ReportTemplates.Add(new ReportTemplate
        {
            InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit,
            Target = ReportTemplateTarget.Correspondence, Name = "قديم",
            LayoutJson = """[{"type":"logo"},{"type":"header","text":"دائرة الموارد البشرية"},{"type":"divider"}]""",
        });
        await db.SaveChangesAsync();

        await Migrator(db).RunAsync();

        var row = await db.ReportTemplates.SingleAsync();
        Assert.Equal(LetterheadStatus.Active, row.Status);
        var slots = JsonSerializer.Deserialize<DepartmentSlots>(row.SlotsJson!, Json)!;
        Assert.Equal("دائرة الموارد البشرية", slots.DepartmentLine);
    }

    [Fact]
    public async Task Blocks_the_fixed_layout_now_owns_are_reported_not_dropped_silently()
    {
        using var db = NewDb();
        db.ReportTemplates.Add(new ReportTemplate
        {
            InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit,
            Target = ReportTemplateTarget.Correspondence, Name = "قديم",
            LayoutJson = """[{"type":"header","text":"قسم"},{"type":"table"},{"type":"fields"}]""",
        });
        await db.SaveChangesAsync();

        await Migrator(db).RunAsync();

        var row = await db.ReportTemplates.SingleAsync();
        Assert.Equal(LetterheadStatus.Active, row.Status);
        Assert.Contains("جدول", row.ReviewNote);
        Assert.Contains("حقول", row.ReviewNote);
    }

    [Fact]
    public async Task Migration_is_idempotent()
    {
        using var db = NewDb();
        db.ReportTemplates.Add(new ReportTemplate
        {
            InstitutionId = 1, Scope = ReportTemplateScope.OrgUnit,
            Target = ReportTemplateTarget.Correspondence, Name = "قديم",
            LayoutJson = """[{"type":"header","text":"قسم أ"}]""",
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await Migrator(db).RunAsync());
        Assert.Equal(0, await Migrator(db).RunAsync());   // second pass finds nothing to do
    }

    [Fact]
    public async Task Document_designs_are_left_alone()
    {
        using var db = NewDb();
        db.ReportTemplates.Add(new ReportTemplate
        {
            InstitutionId = 1, Scope = ReportTemplateScope.Default,
            Target = ReportTemplateTarget.Document, Name = "تقرير",
            LayoutJson = """[{"type":"header","text":"تقرير"}]""",
        });
        await db.SaveChangesAsync();

        await Migrator(db).RunAsync();

        var row = await db.ReportTemplates.SingleAsync();
        Assert.Null(row.SlotsJson);
        Assert.Null(row.LegacyLayoutJson);
    }

    // ── Body handling ────────────────────────────────────────────────────────────

    [Fact]
    public void Legacy_html_body_is_flattened_to_plain_paragraphs()
    {
        var text = LetterheadService.PlainText("<p>سطر أول</p><p><b>سطر</b> ثانٍ</p>");
        var paragraphs = LetterheadRenderer.SplitParagraphs(text);

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("سطر أول", paragraphs[0]);
        Assert.Equal("سطر ثانٍ", paragraphs[1]);
    }

    [Fact]
    public void Html_entities_survive_flattening() =>
        Assert.Equal("الرسوم & الضرائب", LetterheadService.PlainText("<p>الرسوم &amp; الضرائب</p>"));

    // ── Golden PDF ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The test that actually proves the renderer works: generate a letter and read the Arabic
    /// back out of the PDF. Without the vendored font this fails — QuestPDF would fall back to a
    /// Latin face and the Arabic would be missing glyphs.
    ///
    /// Extraction returns Arabic *presentation forms* (U+FB50–U+FEFF) in visual order, so both
    /// sides are NFKC-normalised and compared as character sets rather than substrings.
    /// </summary>
    [Fact]
    public void Rendered_letter_contains_its_arabic_content()
    {
        LetterheadRenderer.RegisterFonts();

        var letterhead = new ResolvedLetterhead(
            OrgSlots.Default,
            new DepartmentSlots("دائرة الشؤون المالية", OrgSlots.Default.ApprovedPalette[1]),
            new SignerSlots("سليمان", "مدير الدائرة", "s@x.gov"));

        var content = new LetterContent(
            RegisterNumber: "DIW-2026-0142",
            Subject: "بشأن تنظيم أرشيف الدائرة",
            MailDate: "2026-08-01",
            SentDate: null,
            RecipientOrg: "وزارة الاتصالات",
            AddresseeName: "اسم المخاطَب",
            AddresseeTitle: "مدير عام",
            Body: "تحية طيبة وبعد،\nيسرنا إحاطتكم علمًا بإنجاز جرد السجلات.",
            Classification: null,
            FileNumber: null);

        var pdf = new LetterheadRenderer().Render(letterhead, content, "مؤسسة همم");

        Assert.NotEmpty(pdf);
        using var doc = PdfDocument.Open(pdf);
        Assert.Equal(1, doc.NumberOfPages);

        var extracted = string.Concat(doc.GetPages().Select(p => p.Text))
            .Normalize(NormalizationForm.FormKC);

        AssertRendered("مؤسسة همم", extracted);
        AssertRendered("دائرة الشؤون المالية", extracted);
        AssertRendered("بشأن تنظيم أرشيف الدائرة", extracted);
        AssertRendered("وزارة الاتصالات", extracted);
        AssertRendered("سليمان", extracted);

        // The reference number is a Latin run inside RTL text; it must not be reordered.
        Assert.Contains("DIW-2026-0142", extracted);
    }

    /// <summary>
    /// The bug this catches: an ISO date inside an RTL paragraph gets its hyphen-separated
    /// segments reversed by the bidi algorithm, so 2026-08-01 prints as 01-08-2026 — a wrong date
    /// on an official letter. The renderer wraps such runs in LTR isolates to prevent it.
    /// </summary>
    [Fact]
    public void Dates_are_not_reordered_by_bidi()
    {
        LetterheadRenderer.RegisterFonts();

        var content = new LetterContent(
            "DIW-2026-0142", "موضوع", "2026-08-01", "2026-08-03",
            "جهة", null, null, "نص", null, null);

        var pdf = new LetterheadRenderer().Render(
            new ResolvedLetterhead(OrgSlots.Default, new DepartmentSlots(null, null), new SignerSlots(null, null, null)),
            content, "مؤسسة");

        using var doc = PdfDocument.Open(pdf);
        var text = string.Concat(doc.GetPages().Select(p => p.Text));

        Assert.Contains("2026-08-01", text);
        Assert.DoesNotContain("01-08-2026", text);
    }

    [Fact]
    public void An_empty_body_still_renders_a_page()
    {
        LetterheadRenderer.RegisterFonts();

        var pdf = new LetterheadRenderer().Render(
            new ResolvedLetterhead(OrgSlots.Default, new DepartmentSlots(null, null), new SignerSlots(null, null, null)),
            new LetterContent("X-1", "موضوع", "2026-08-01", null, null, null, null, "", null, null),
            "مؤسسة");

        using var doc = PdfDocument.Open(pdf);
        Assert.Equal(1, doc.NumberOfPages);
    }

    [Fact]
    public void A_broken_logo_prints_without_one_instead_of_throwing()
    {
        LetterheadRenderer.RegisterFonts();

        var org = OrgSlots.Default with { LogoBase64 = "data:image/png;base64,not-base64!!" };
        var pdf = new LetterheadRenderer().Render(
            new ResolvedLetterhead(org, new DepartmentSlots(null, null), new SignerSlots(null, null, null)),
            new LetterContent("X-1", "موضوع", "2026-08-01", null, null, null, null, "نص", null, null),
            "مؤسسة");

        Assert.NotEmpty(pdf);
    }

    private static void AssertRendered(string expected, string normalizedExtract)
    {
        foreach (var ch in expected.Normalize(NormalizationForm.FormKC).Where(char.IsLetter))
            Assert.True(normalizedExtract.Contains(ch),
                $"'{expected}' did not render — missing '{ch}'. The Arabic font is probably not registered.");
    }
}

/// <summary>Approval is exercised through the real engine elsewhere; these tests only need
/// SaveDepartmentSlots to reach the "no route configured" branch deterministically.</summary>
internal sealed class NoopWorkflowService : IWorkflowService
{
    public Task<IReadOnlyList<Archiving.Application.Features.Workflow.WorkflowDefinitionListItem>> ListDefinitionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Archiving.Application.Features.Workflow.WorkflowDefinitionListItem>>([]);

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowDefinitionDto>> GetDefinitionAsync(long id, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowDefinitionDto>> CreateDefinitionAsync(Archiving.Application.Features.Workflow.CreateWorkflowDefinitionRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowInstanceDto>> StartAsync(Archiving.Application.Features.Workflow.StartWorkflowRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowInstanceDto>> GetInstanceAsync(long id, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<Archiving.Application.Features.Workflow.WorklistItem>> MyWorklistAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Archiving.Application.Features.Workflow.WorklistItem>>([]);

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowInstanceDto>> ActOnTaskAsync(long taskId, Archiving.Application.Features.Workflow.WorkflowActionRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    // Internal-mail integration: composing mail asks the engine whether a route is bound to the
    // category. Answering "none" is exactly what these tests want.
    public Task<Archiving.Application.Features.Workflow.WorkflowInstanceDto?> StartForInternalMailAsync(long internalMailId, long? mailCategoryId, CancellationToken ct = default)
        => Task.FromResult<Archiving.Application.Features.Workflow.WorkflowInstanceDto?>(null);

    public Task<Archiving.Application.Common.Models.Result<Archiving.Application.Features.Workflow.WorkflowInstanceDto>> ResubmitAsync(long instanceId, string? note, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Archiving.Application.Features.Workflow.WorkflowMailContextDto?> ContextForInternalMailAsync(long internalMailId, CancellationToken ct = default)
        => Task.FromResult<Archiving.Application.Features.Workflow.WorkflowMailContextDto?>(null);

    public Task<IReadOnlyDictionary<long, Archiving.Application.Features.Workflow.WorkflowMailBadge>> BadgesForMailsAsync(IReadOnlyCollection<long> internalMailIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<long, Archiving.Application.Features.Workflow.WorkflowMailBadge>>(
            new Dictionary<long, Archiving.Application.Features.Workflow.WorkflowMailBadge>());
}
