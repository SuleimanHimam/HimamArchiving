using System.Globalization;
using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Core;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Supply;

/// <summary>
/// الأرصدة الافتتاحية (§5.3, slide 35): template → upload → automatic check with an error report → SUP-OPN
/// into approval → balances and custody posted on approval → lock. May run in stages (one department after
/// another); the lock comes after the last. After the lock only a count adjustment changes stock.
/// </summary>
public sealed class OpeningBalanceService(AppDbContext db, IAuditWriter audit) : IOpeningBalanceService
{
    private const int MaxRows = 20000;

    public string Template() => "﻿" + OpeningTemplate.Header + "\n";

    public async Task<OpeningStatusDto> StatusAsync(CancellationToken ct = default)
    {
        var s = await SupplyCommon.SettingsAsync(db, ct);
        DateOnly? locked = DateOnly.TryParse(s.GetValueOrDefault(SupplySettingKeys.OpeningLockedOn), CultureInfo.InvariantCulture, out var d) ? d : null;
        var rows = await db.OpeningImports.AsNoTracking().OrderByDescending(i => i.Id).Take(50).ToListAsync(ct);
        var docIds = rows.Where(i => i.DocumentId != null).Select(i => i.DocumentId!.Value).ToList();
        var docs = await db.SupplyDocuments.AsNoTracking().Where(x => docIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return new OpeningStatusDto(locked is not null, locked, rows.Select(i => new OpeningImportDto(i.Id, i.FileName, i.Stage, i.RowCount,
            i.Status.ToString(), JsonSerializer.Deserialize<List<OpeningErrorDto>>(i.ErrorsJson) ?? [], i.DocumentId,
            i.DocumentId is { } x ? docs.GetValueOrDefault(x)?.Number : null, i.DocumentId is { } y ? docs.GetValueOrDefault(y)?.Status.ToString() : null,
            i.CreatedAt)).ToList());
    }

    public async Task<Result<OpeningImportDto>> UploadAsync(UploadOpeningRequest r, CancellationToken ct = default)
    {
        var s = await SupplyCommon.SettingsAsync(db, ct);
        if (s.GetValueOrDefault(SupplySettingKeys.OpeningLockedOn) is { Length: > 0 })
            return Result<OpeningImportDto>.Fail("تاريخ الافتتاح مقفل: ما يُكتشف لاحقاً يدخل بمحضر تسوية جرد");
        var fileName = Path.GetFileName(CoreGuards.Clean(r.FileName));
        var stage = CoreGuards.Clean(r.Stage);
        if (fileName.Length is 0 or > 255) return Result<OpeningImportDto>.Fail("اسم الملف غير صالح");
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return Result<OpeningImportDto>.Fail("القالب ملف CSV (احفظه من Excel بصيغة CSV UTF-8)");
        if (stage.Length is 0 or > 200) return Result<OpeningImportDto>.Fail("حدّد المرحلة (مثلاً اسم الدائرة أو المستودع)");
        if (r.Content.Length > 10_000_000) return Result<OpeningImportDto>.Fail("الملف أكبر من المسموح");

        var (rows, errors) = OpeningTemplate.Parse(r.Content);
        var list = errors.Select(e => new OpeningErrorDto(e.Line, e.Message)).ToList();
        if (rows.Count > MaxRows) list.Add(new OpeningErrorDto(0, $"أكثر من {MaxRows} سطر: قسّم الملف على مراحل"));

        var lines = new List<SupplyDocumentLine>();
        if (list.Count == 0) lines = await ResolveAsync(rows, list, ct);

        var import = new OpeningImport
        {
            FileName = fileName, Stage = stage, RowCount = rows.Count,
            Status = list.Count == 0 ? OpeningImportStatus.Valid : OpeningImportStatus.Failed,
            ErrorsJson = JsonSerializer.Serialize(list.Take(1000)),
        };
        db.OpeningImports.Add(import);
        await db.SaveChangesAsync(ct);

        if (list.Count == 0)
        {
            var doc = new SupplyDocument
            {
                Type = DocumentType.OPN, DocDate = SupplyCommon.Today(), OpeningImportId = import.Id,
                Title = $"رصيد افتتاحي — {stage}", Notes = fileName,
            };
            foreach (var l in lines) doc.Lines.Add(l);
            db.SupplyDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            import.DocumentId = doc.Id;
            import.Status = OpeningImportStatus.DocumentCreated;
            await db.SaveChangesAsync(ct);
        }
        await audit.WriteAsync("Imported", "OpeningImport", import.Id, fileName, newValues: $"rows={rows.Count}, errors={list.Count}", ct: ct);
        var status = await StatusAsync(ct);
        return Result<OpeningImportDto>.Ok(status.Imports.First(i => i.Id == import.Id));
    }

    public async Task<Result<OpeningStatusDto>> LockAsync(CancellationToken ct = default)
    {
        var rows = await db.SupplySettings.ToDictionaryAsync(x => x.Key, ct);
        if (rows.TryGetValue(SupplySettingKeys.OpeningLockedOn, out var row) && row.Value.Length > 0)
            return Result<OpeningStatusDto>.Fail("تاريخ الافتتاح مقفل مسبقاً");
        // Nothing may be half-way: every opening document is either executed or closed.
        var pending = await db.SupplyDocuments.AnyAsync(d => d.Type == DocumentType.OPN
            && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.Returned), ct);
        if (pending) return Result<OpeningStatusDto>.Fail("يوجد محضر رصيد افتتاحي لم يُعتمد أو يُلغَ بعد");
        if (!await db.SupplyDocuments.AnyAsync(d => d.Type == DocumentType.OPN && d.Status == DocumentStatus.Executed, ct))
            return Result<OpeningStatusDto>.Fail("لا يوجد رصيد افتتاحي معتمد بعد");
        var today = SupplyCommon.Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (row is null) db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.OpeningLockedOn, Value = today });
        else row.Value = today;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Locked", "OpeningBalance", 0, "قفل تاريخ الافتتاح", newValues: today, ct: ct);
        return Result<OpeningStatusDto>.Ok(await StatusAsync(ct));
    }

    /// <summary>The checks that need the database: unknown item / warehouse / employee / unit, serial or barcode already used.</summary>
    private async Task<List<SupplyDocumentLine>> ResolveAsync(IReadOnlyList<OpeningTemplate.Row> rows, List<OpeningErrorDto> errors, CancellationToken ct)
    {
        var items = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Code, StringComparer.OrdinalIgnoreCase, ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.IsActive).ToDictionaryAsync(w => w.Code, StringComparer.OrdinalIgnoreCase, ct);
        var employees = await db.Employees.AsNoTracking().Where(e => e.Status == EmployeeStatus.Active)
            .ToDictionaryAsync(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase, ct);
        var units = (await db.OrgUnits.AsNoTracking().Where(u => u.Code != null).ToListAsync(ct))
            .GroupBy(u => u.Code!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var serials = rows.Where(r => r.Serial != null).Select(r => r.Serial!).ToList();
        var usedSerials = (await db.Assets.AsNoTracking().Where(a => a.SerialNumber != null && serials.Contains(a.SerialNumber)).Select(a => a.SerialNumber!)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codes = rows.Where(r => r.Barcode != null).Select(r => r.Barcode!).ToList();
        var usedCodes = (await db.Assets.AsNoTracking().Where(a => codes.Contains(a.Barcode)).Select(a => a.Barcode).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lines = new List<SupplyDocumentLine>();
        foreach (var r in rows)
        {
            if (!items.TryGetValue(r.ItemCode, out var item)) { errors.Add(new(r.Line, $"صنف غير معرّف ({r.ItemCode})")); continue; }
            long? wh = null;
            if (r.WarehouseCode is { } wc)
            {
                if (!warehouses.TryGetValue(wc, out var w)) { errors.Add(new(r.Line, $"مستودع غير معرّف ({wc})")); continue; }
                wh = w.Id;
            }
            long? holderEmp = null, holderUnit = null;
            if (r.HolderEmployeeNumber is { } en)
            {
                if (!employees.TryGetValue(en, out var e)) { errors.Add(new(r.Line, $"موظف غير موجود ({en})")); continue; }
                holderEmp = e.Id;
            }
            if (r.HolderOrgUnitCode is { } uc)
            {
                if (!units.TryGetValue(uc, out var u)) { errors.Add(new(r.Line, $"دائرة غير معرّفة ({uc})")); continue; }
                holderUnit = u.Id;
            }
            if (holderEmp is not null && holderUnit is not null) { errors.Add(new(r.Line, "صاحب العهدة موظف أو دائرة، لا الاثنان")); continue; }

            if (item.Kind == ItemKind.FixedAsset)
            {
                if (r.Quantity != 1) { errors.Add(new(r.Line, "الأصل الثابت سطر لكل قطعة (الكمية 1)")); continue; }
                if (wh is null && holderEmp is null && holderUnit is null) { errors.Add(new(r.Line, "حدّد المستودع أو صاحب العهدة")); continue; }
                if (r.Serial is { } sn && usedSerials.Contains(sn)) { errors.Add(new(r.Line, $"رقم تسلسلي مسجّل مسبقاً ({sn})")); continue; }
                if (r.Barcode is { } bc && usedCodes.Contains(bc)) { errors.Add(new(r.Line, $"باركود مستخدم مسبقاً ({bc})")); continue; }
            }
            else
            {
                if (wh is null) { errors.Add(new(r.Line, "المستهلكات تُسجّل على مستودع")); continue; }
                if (holderEmp is not null || holderUnit is not null) { errors.Add(new(r.Line, "المستهلكات لا تكون عهدة")); continue; }
                if (r.Serial is not null || r.Barcode is not null) { errors.Add(new(r.Line, "الرقم التسلسلي والباركود للأصول فقط")); continue; }
            }
            lines.Add(new SupplyDocumentLine
            {
                ItemId = item.Id, Quantity = r.Quantity, SerialNumber = r.Serial, Barcode = r.Barcode, Condition = r.Condition,
                UnitValue = r.Value, PurchaseDate = r.PurchaseDate, WarehouseId = wh, HolderEmployeeId = holderEmp, HolderOrgUnitId = holderUnit,
            });
        }
        return lines;
    }
}
