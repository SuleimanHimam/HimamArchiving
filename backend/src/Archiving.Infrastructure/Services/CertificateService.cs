using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Archiving.Infrastructure.Services;

/// <summary>Renders the Certificate of Destruction as PDF/A-2b and stores it in object storage.</summary>
public sealed class CertificateService(AppDbContext db, IFileStorage storage) : ICertificateService
{
    private sealed record ItemLine(string DocNumber, string Title, string Method, string? Checksum);

    public async Task<long> IssueAsync(long destructionRequestId, CancellationToken ct = default)
    {
        var req = await db.DestructionRequests.Include(r => r.Items).FirstAsync(r => r.Id == destructionRequestId, ct);
        var org = await db.Institutions.Select(i => i.Name).FirstOrDefaultAsync(ct) ?? "المؤسسة";

        var docIds = req.Items.Select(i => i.DocumentId).ToList();
        var docs = await db.Documents.IgnoreQueryFilters().Where(d => docIds.Contains(d.Id))
            .Select(d => new { d.Id, d.DocumentNumber, d.Title }).ToListAsync(ct);
        string? Name(long? uid) => uid is null ? null
            : db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => u.FullName).FirstOrDefault();

        var lines = req.Items.Select(i =>
        {
            var d = docs.FirstOrDefault(x => x.Id == i.DocumentId);
            var method = !string.IsNullOrEmpty(i.CustomMethod) ? i.CustomMethod : i.Method.ToString();
            return new ItemLine(d?.DocumentNumber ?? $"#{i.DocumentId}", d?.Title ?? "—", method, i.ChecksumBefore);
        }).ToList();

        var certNo = $"DEST-{DateTime.UtcNow:yyyy}-{req.Id:D4}";
        var pdf = Render(certNo, org, req, lines, Name(req.RequestedByUserId), Name(req.ApprovedByUserId), Name(req.ExecutedByUserId));

        StoredFile stored;
        using (var ms = new MemoryStream(pdf)) stored = await storage.SaveAsync("certificates", $"{certNo}.pdf", ms, ct);

        var cert = new DestructionCertificate
        {
            DestructionRequestId = req.Id, CertificateNumber = certNo, PdfStorageKey = stored.StorageKey,
        };
        db.DestructionCertificates.Add(cert);
        await db.SaveChangesAsync(ct);
        req.CertificateId = cert.Id;
        await db.SaveChangesAsync(ct);
        return cert.Id;
    }

    public async Task<(Stream Stream, string FileName)?> OpenAsync(long destructionRequestId, CancellationToken ct = default)
    {
        var cert = await db.DestructionCertificates
            .Where(c => c.DestructionRequestId == destructionRequestId)
            .OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (cert?.PdfStorageKey is null) return null;
        var stream = await storage.OpenAsync(cert.PdfStorageKey, ct);
        return stream is null ? null : (stream, $"{cert.CertificateNumber}.pdf");
    }

    private static byte[] Render(string certNo, string org, DestructionRequest req, List<ItemLine> items,
        string? requester, string? approver, string? executor)
    {
        var doc = QuestPDF.Fluent.Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(11));
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text(org).Bold().FontSize(16);
                    col.Item().AlignCenter().Text("شهادة إتلاف وثائق").FontSize(14).Bold();
                    col.Item().AlignCenter().Text($"رقم الشهادة: {certNo}").FontSize(10);
                    col.Item().PaddingTop(8).Text($"رقم الطلب: {req.Id}");
                    col.Item().Text($"السبب: {req.Reason}");
                    col.Item().Text($"طلب الإتلاف: {requester ?? "—"} — {req.RequestedAtUtc:yyyy-MM-dd HH:mm} UTC");
                    col.Item().Text($"الاعتماد: {approver ?? "—"} — {req.ApprovedAtUtc:yyyy-MM-dd HH:mm} UTC");
                    col.Item().Text($"التنفيذ: {executor ?? "—"} — {req.ExecutedAtUtc:yyyy-MM-dd HH:mm} UTC");

                    col.Item().PaddingTop(10).Text("الوثائق المُتلَفة:").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(3); cd.RelativeColumn(2); cd.RelativeColumn(5); });
                        table.Header(h =>
                        {
                            h.Cell().Text("الرقم").Bold();
                            h.Cell().Text("العنوان").Bold();
                            h.Cell().Text("الطريقة").Bold();
                            h.Cell().Text("البصمة قبل الإتلاف (SHA-256)").Bold();
                        });
                        foreach (var it in items)
                        {
                            table.Cell().Text(it.DocNumber);
                            table.Cell().Text(it.Title);
                            table.Cell().Text(it.Method);
                            table.Cell().Text(it.Checksum ?? "—").FontSize(7);
                        }
                    });

                    col.Item().PaddingTop(16).Text(
                        "تشهد هذه الوثيقة بأن السجلات المذكورة أعلاه قد أُتلِفت بصورة آمنة وغير قابلة للاسترجاع، " +
                        "وأن البيانات الوصفية (الشاهدة) محفوظة كإثبات على وجودها السابق وإتلافها النظامي.").Italic();
                });
                page.Footer().AlignCenter().Text($"صدرت آليًا · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });
        }).WithSettings(new DocumentSettings { PDFA_Conformance = PDFA_Conformance.PDFA_2B });

        return doc.GeneratePdf();
    }
}
