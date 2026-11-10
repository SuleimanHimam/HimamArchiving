using System.Reflection;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Letterhead;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// The single master layout every outgoing Diwan letter is rendered through.
///
/// The zones below are fixed and not configurable: header, reference/date, recipient, body,
/// footer. Slot values only choose the words and the one approved accent colour that go inside
/// them. That is the whole mechanism by which two departments' letters stay recognisably the
/// same institution's — a template cannot move, resize or restyle anything.
/// </summary>
public sealed class LetterheadRenderer : ILetterheadRenderer
{
    /// <summary>
    /// Arabic is laid out right-to-left, but a reference number or an ISO date is a left-to-right
    /// run inside it. Without an explicit isolate the bidi algorithm reorders the hyphen-separated
    /// segments and "2026-08-01" prints as "01-08-2026" — a wrong date on an official letter, and
    /// invisible to any test that only checks the characters are present.
    /// </summary>
    private static string Ltr(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"⁦{value}⁩";

    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    /// <summary>
    /// QuestPDF refuses to generate without a declared licence, and the Arabic faces must be
    /// registered before the first render. Both happen in the type initialiser so that <em>any</em>
    /// consumer — the API, a test, a future background job — gets a working renderer without a
    /// separate setup call it could forget. Relying on DI for this meant a renderer constructed
    /// outside the container silently produced Latin-only output.
    /// </summary>
    static LetterheadRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterFonts();
    }

    /// <summary>Registers the vendored Arabic faces once per process. They ship as embedded
    /// resources, so rendering does not depend on the working directory or on a font being
    /// installed on the host — a Linux container has neither.</summary>
    public static void RegisterFonts()
    {
        lock (FontLock)
        {
            if (_fontsRegistered) return;
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames()
                         .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is not null) FontManager.RegisterFont(stream);
            }
            _fontsRegistered = true;
        }
    }

    public byte[] Render(ResolvedLetterhead lh, LetterContent content, string institutionName)
    {
        var org = lh.Org;
        var accent = Hex(lh.EffectiveAccentHex, org.BrandAccentHex);
        var primary = Hex(org.BrandPrimaryHex, "#14213D");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSize(org.PageSize));
                page.MarginTop(org.MarginTopMm, Unit.Millimetre);
                page.MarginBottom(org.MarginBottomMm, Unit.Millimetre);
                // "Inner/outer" rather than left/right: on an RTL page the binding edge is the
                // right one, so naming them by side would invert the meaning for Arabic.
                page.MarginRight(org.MarginInnerMm, Unit.Millimetre);
                page.MarginLeft(org.MarginOuterMm, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontFamily(org.BaseFontFamily).FontSize(org.BaseFontPt));
                page.ContentFromRightToLeft();

                page.Header().Element(e => Header(e, lh, institutionName, primary, accent));
                page.Content().Element(e => Body(e, content, primary));
                page.Footer().Element(e => Footer(e, lh, accent));
            });
        }).WithSettings(new DocumentSettings { PDFA_Conformance = PDFA_Conformance.PDFA_2B });

        return doc.GeneratePdf();
    }

    // ── Header zone: logo, institution, department line, accent rule ──────────────
    private static void Header(IContainer c, ResolvedLetterhead lh, string institutionName,
        string primary, string accent)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                var logo = DecodeLogo(lh.Org.LogoBase64);
                if (logo is not null) row.ConstantItem(90).AlignMiddle().Image(logo).FitArea();

                row.RelativeItem().Column(text =>
                {
                    text.Item().Text(institutionName).Bold().FontSize(16).FontColor(primary);
                    if (!string.IsNullOrWhiteSpace(lh.Department.DepartmentLine))
                        text.Item().Text(lh.Department.DepartmentLine!).FontSize(11).FontColor(primary);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(accent);
        });
    }

    // ── Reference/date, recipient block, then the body ───────────────────────────
    private static void Body(IContainer c, LetterContent content, string primary)
    {
        c.PaddingVertical(12).Column(col =>
        {
            col.Spacing(8);

            // Reference zone. Both values are LTR runs inside an RTL line — see Ltr().
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("الرقم: ").SemiBold();
                    t.Span(Ltr(content.RegisterNumber));
                });
                row.RelativeItem().AlignLeft().Text(t =>
                {
                    t.Span("التاريخ: ").SemiBold();
                    t.Span(Ltr(content.MailDate));
                });
            });

            if (!string.IsNullOrWhiteSpace(content.SentDate))
                col.Item().Text(t =>
                {
                    t.Span("تاريخ الإرسال: ").SemiBold();
                    t.Span(Ltr(content.SentDate));
                });

            // Recipient block.
            col.Item().PaddingTop(6).Column(rec =>
            {
                if (!string.IsNullOrWhiteSpace(content.RecipientOrg))
                    rec.Item().Text(content.RecipientOrg!).Bold().FontSize(12).FontColor(primary);
                if (!string.IsNullOrWhiteSpace(content.AddresseeName))
                    rec.Item().Text(content.AddresseeName!);
                if (!string.IsNullOrWhiteSpace(content.AddresseeTitle))
                    rec.Item().Text(content.AddresseeTitle!).FontSize(10);
            });

            col.Item().PaddingTop(4).Text(t =>
            {
                t.Span("الموضوع: ").Bold();
                t.Span(content.Subject).Bold();
            });

            // Body zone. Plain text by design: paragraphs split on newlines, no inline markup,
            // so nothing a user types can alter the letter's appearance.
            col.Item().PaddingTop(8).Column(body =>
            {
                body.Spacing(6);
                foreach (var para in SplitParagraphs(content.Body))
                    body.Item().Text(para).LineHeight(1.6f);
            });

            if (!string.IsNullOrWhiteSpace(content.FileNumber))
                col.Item().PaddingTop(10).Text(t =>
                {
                    t.Span("رقم الملف: ").SemiBold().FontSize(9);
                    t.Span(Ltr(content.FileNumber)).FontSize(9);
                });
        });
    }

    // ── Footer: signer block (user slots) then the institution's legal line ──────
    private static void Footer(IContainer c, ResolvedLetterhead lh, string accent)
    {
        c.Column(col =>
        {
            col.Item().PaddingBottom(4).LineHorizontal(0.75f).LineColor(accent);

            var s = lh.Signer;
            if (!string.IsNullOrWhiteSpace(s.SignerName))
                col.Item().Text(s.SignerName!).SemiBold().FontSize(10);
            if (!string.IsNullOrWhiteSpace(s.SignerTitle))
                col.Item().Text(s.SignerTitle!).FontSize(9);
            if (!string.IsNullOrWhiteSpace(s.SignerContact))
                col.Item().Text(Ltr(s.SignerContact)).FontSize(9);

            if (!string.IsNullOrWhiteSpace(lh.Org.LegalFooterAr))
                col.Item().PaddingTop(4).Text(lh.Org.LegalFooterAr!).FontSize(8).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(lh.Org.LegalFooterEn))
                col.Item().Text(Ltr(lh.Org.LegalFooterEn)).FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Blank lines separate paragraphs; a lone newline is still a break. An empty body
    /// yields one empty paragraph so the zone keeps its height instead of collapsing.</summary>
    internal static IReadOnlyList<string> SplitParagraphs(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return [string.Empty];
        return body.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .DefaultIfEmpty(string.Empty)
            .ToList();
    }

    /// <summary>A malformed colour must not take a letter down; fall back to the brand value.</summary>
    private static string Hex(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
        && System.Text.RegularExpressions.Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            ? value
            : fallback;

    private static byte[]? DecodeLogo(string? dataUri)
    {
        if (string.IsNullOrWhiteSpace(dataUri)) return null;
        var comma = dataUri.IndexOf(',');
        var payload = comma >= 0 ? dataUri[(comma + 1)..] : dataUri;
        try { return Convert.FromBase64String(payload); }
        catch (FormatException) { return null; }   // a broken logo prints as no logo, not as a 500
    }

    private static PageSize PageSize(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "A5" => PageSizes.A5,
        "LETTER" => PageSizes.Letter,
        _ => PageSizes.A4,
    };
}
