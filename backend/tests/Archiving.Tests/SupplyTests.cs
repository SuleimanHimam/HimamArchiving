using System.Reflection;
using Archiving.Api.Authorization;
using Archiving.Api.Controllers.Supply;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Services;
using Archiving.Infrastructure.Services.Supply;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DocumentType = Archiving.Domain.Supply.DocumentType;

namespace Archiving.Tests;

/// <summary>اللوازم والمخازن. Every item, figure and name is illustrative test data.</summary>
public partial class SupplyTests
{
    private const long Unit = 10, Workflow = 7, W1 = 100, W2 = 200, WM = 300;

    private sealed class Scope(bool admin = true, params string[] perms) : IAccessScope
    {
        public Task<bool> IsSystemAdminAsync(CancellationToken ct = default) => Task.FromResult(admin);
        public Task<bool> HasPermissionAsync(string code, CancellationToken ct = default) => Task.FromResult(admin || perms.Contains(code));
        public Task<long?> OrgUnitIdAsync(CancellationToken ct = default) => Task.FromResult<long?>(Unit);
        public Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([Unit]);
        public Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([]);
        public Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default) => Task.FromResult(admin);
    }

    private sealed class StubApprovals : IApprovalService
    {
        public Task<Result<ApprovalRequestDto>> SubmitAsync(SubmitApprovalRequest r, CancellationToken ct = default) =>
            Task.FromResult(Result<ApprovalRequestDto>.Ok(new ApprovalRequestDto(0, "REQ", r.RequestType, r.SourceModule, r.SourceId, r.Title,
                "Pending", null, DateTime.UtcNow, null, null)));
        public Task<IReadOnlyList<ApprovalRequestDto>> ListMineAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalRequestDto>>([]);
        public Task<IReadOnlyList<ApprovalInboxItem>> InboxAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalInboxItem>>([]);
    }

    private sealed record Fixture(AppDbContext Db, Item Laptop, Item Paper, Item Fuse, Employee Keeper, Employee Staff);

    private static Fixture NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor(new TestCurrentUser())).Options);
        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.OrgUnits.Add(new OrgUnit { Id = Unit, InstitutionId = 1, Name = "دائرة أ", Code = "U-A" });
        db.Users.Add(new User { Id = 5, FullName = "أمين تجريبي", Email = "k@test.invalid" });
        db.Users.Add(new User { Id = 6, FullName = "موظف تجريبي", Email = "s@test.invalid" });
        db.WorkflowDefinitions.Add(new WorkflowDefinition { Id = Workflow, Name = "مسار تجريبي", IsActive = true });
        foreach (var t in Enum.GetNames<DocumentType>())
        {
            db.NumberingTemplates.Add(new NumberingTemplate { EntityType = $"SupplyDocument.{t}", Pattern = $"SUP-{t}-{{YYYY}}-{{SEQ:5}}" });
            db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.Workflow(t), Value = Workflow.ToString() });
        }
        db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.IssueRequestWorkflow, Value = Workflow.ToString() });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = "SupplyIssueRequest", Pattern = "SR-{YYYY}-{SEQ:5}" });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = "SupplyCount", Pattern = "CNT-{YYYY}-{SEQ:3}" });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = "SupplyWorkOrder", Pattern = "WO-{YYYY}-{SEQ:5}" });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = "SupplyAsset", Pattern = "AST-{SEQ:5}" });
        db.NumberingTemplates.Add(new NumberingTemplate { EntityType = "SupplyBarcode", Pattern = "{SEQ:8}" });

        var keeper = new Employee { EmployeeNumber = "T-1", FullName = "أمين أ", OrgUnitId = Unit, JobTitle = "أمين", UserId = 5 };
        var staff = new Employee { EmployeeNumber = "T-2", FullName = "موظف ب", OrgUnitId = Unit, JobTitle = "موظف", UserId = 6 };
        db.Employees.AddRange(keeper, staff);
        db.SaveChanges();
        db.Warehouses.Add(new Warehouse { Id = W1, Code = "W1", Name = "مستودع أ", KeeperEmployeeId = keeper.Id });
        db.Warehouses.Add(new Warehouse { Id = W2, Code = "W2", Name = "مستودع ب" });
        db.Warehouses.Add(new Warehouse { Id = WM, Code = "WM", Name = "مستودع الصيانة", Kind = WarehouseKind.Maintenance, KeeperEmployeeId = keeper.Id });
        var assets = new ItemCategory { Code = "C-A", Name = "أجهزة", Kind = ItemKind.FixedAsset };
        var consumables = new ItemCategory { Code = "C-C", Name = "قرطاسية", Kind = ItemKind.Consumable };
        var parts = new ItemCategory { Code = "C-M", Name = "قطع", Kind = ItemKind.MaintenanceSupply };
        db.ItemCategories.AddRange(assets, consumables, parts);
        db.SaveChanges();
        var laptop = new Item { Code = "I-1", Name = "جهاز تجريبي", CategoryId = assets.Id, Kind = ItemKind.FixedAsset, Unit = "جهاز", WarrantyMonths = 12 };
        var paper = new Item { Code = "I-2", Name = "ورق تجريبي", CategoryId = consumables.Id, Kind = ItemKind.Consumable, Unit = "رزمة", ReorderLevel = 5 };
        var fuse = new Item { Code = "I-3", Name = "قطعة تجريبية", CategoryId = parts.Id, Kind = ItemKind.MaintenanceSupply, Unit = "حبة" };
        db.Items.AddRange(laptop, paper, fuse);
        db.SaveChanges();
        return new Fixture(db, laptop, paper, fuse, keeper, staff);
    }

    private static NumberingService Numbering(AppDbContext db) => new(db, new NoopAuditWriter());
    private static SupplyDocumentService Documents(AppDbContext db, IAccessScope? scope = null) =>
        new(db, scope ?? new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(db));
    private static SupplyDocumentEffectHandler DocEffect(AppDbContext db) => new(db, Numbering(db), new NoopAuditWriter());

    private static async Task<SupplyDocumentDto> ApproveAsync(AppDbContext db, long documentId)
    {
        var d = await db.SupplyDocuments.FirstAsync(x => x.Id == documentId);
        await DocEffect(db).OnApprovedAsync(new ApprovalRequest { SourceId = d.Id, RequestType = SupplyDocumentService.RequestType }, default);
        return (await Documents(db).GetAsync(documentId)).Value!;
    }

    private static SaveDocumentRequest Doc(DocumentType type, IReadOnlyList<SaveDocumentLine> lines, long? wh = W1, long? target = null,
        long? employee = null, long? unit = null, string? supplier = null) =>
        new(type.ToString(), new DateOnly(2026, 11, 18), $"محضر {type}", null, wh, target, employee, unit, supplier, null, null, lines);

    private static SaveDocumentLine L(long item, decimal qty, long? asset = null, string? serial = null, string? condition = null, decimal? value = null) =>
        new(item, qty, asset, serial, null, condition, value, null, null, null, null, null);

    /// <summary>Receives two laptops and 20 reams into W1 through an approved SUP-RCV.</summary>
    private static async Task<SupplyDocumentDto> ReceiveAsync(Fixture f)
    {
        var docs = Documents(f.Db);
        var created = await docs.CreateAsync(Doc(DocumentType.RCV,
            [L(f.Laptop.Id, 1, serial: "SN-A", value: 3000), L(f.Laptop.Id, 1, serial: "SN-B", value: 3000), L(f.Paper.Id, 20, value: 10), L(f.Fuse.Id, 10, value: 2)],
            supplier: "مورد تجريبي"));
        Assert.True(created.Succeeded, created.Error);
        Assert.True((await docs.SubmitAsync(created.Value!.Id)).Succeeded);
        return await ApproveAsync(f.Db, created.Value.Id);
    }

    private static Task<decimal> Balance(AppDbContext db, long item, long wh) =>
        db.StockMovements.Where(m => m.ItemId == item && m.WarehouseId == wh).SumAsync(m => m.Quantity);

    // ================= pure rules =================

    [Fact]
    public void Issue_planner_gives_full_partial_or_none()
    {
        var lines = new[] { new IssuePlanner.Line(1, 5), new IssuePlanner.Line(2, 3) };
        Assert.Equal(IssuePlanner.Outcome.Full, IssuePlanner.Plan(lines, new Dictionary<long, decimal> { [1] = 9, [2] = 3 }).Outcome);
        var partial = IssuePlanner.Plan(lines, new Dictionary<long, decimal> { [1] = 2 });
        Assert.Equal(IssuePlanner.Outcome.Partial, partial.Outcome);
        Assert.Equal((1L, 2m), Assert.Single(partial.Issue));
        Assert.Equal(IssuePlanner.Outcome.None, IssuePlanner.Plan(lines, new Dictionary<long, decimal> { [1] = -4 }).Outcome);
    }

    [Fact]
    public void Count_reconciler_finds_every_variance_kind_and_ignores_double_scans()
    {
        var expectedQty = new Dictionary<(long, long), decimal> { [(1, 9)] = 10, [(2, 9)] = 4 };
        var countedQty = new Dictionary<(long, long), decimal> { [(1, 9)] = 8, [(2, 9)] = 7 };
        var expected = new[]
        {
            new CountReconciler.ExpectedAsset(11, "B11", 5, AssetCondition.Good),
            new CountReconciler.ExpectedAsset(12, "B12", 5, AssetCondition.Good),
            new CountReconciler.ExpectedAsset(13, "B13", 5, AssetCondition.Good),
        };
        var scanned = new[]
        {
            new CountReconciler.ScannedAsset("B11", 11, 5, AssetCondition.Good, true),
            new CountReconciler.ScannedAsset("b11", 11, 5, AssetCondition.Good, true),            // scanned twice
            new CountReconciler.ScannedAsset("B12", 12, 5, AssetCondition.Damaged, true),
            new CountReconciler.ScannedAsset("B99", 99, 5, AssetCondition.Good, true),            // belongs elsewhere
            new CountReconciler.ScannedAsset("ZZZ", null, null, null, false),                      // unknown label
        };
        var v = CountReconciler.Compare(expectedQty, countedQty, expected, scanned);
        Assert.Contains(v, x => x is { Kind: VarianceKind.Shortage, ItemId: 1, Expected: 10, Counted: 8 });
        Assert.Contains(v, x => x is { Kind: VarianceKind.Surplus, ItemId: 2, Counted: 7 });
        Assert.Contains(v, x => x is { Kind: VarianceKind.ConditionChanged, AssetId: 12, CountedCondition: AssetCondition.Damaged });
        Assert.Contains(v, x => x is { Kind: VarianceKind.Missing, AssetId: 13 });
        Assert.Contains(v, x => x is { Kind: VarianceKind.Misplaced, AssetId: 99 });
        Assert.Contains(v, x => x is { Kind: VarianceKind.Surplus, Barcode: "ZZZ", ItemId: null });
        Assert.DoesNotContain(v, x => x.AssetId == 11);
        Assert.Equal(6, v.Count);
    }

    [Fact]
    public void Replacement_is_suggested_only_past_a_configured_percentage()
    {
        Assert.True(ReplacementAdvisor.Suggest(2600, 5000, 50));
        Assert.False(ReplacementAdvisor.Suggest(2500, 5000, 50));
        Assert.False(ReplacementAdvisor.Suggest(9999, 5000, null));
        Assert.False(ReplacementAdvisor.Suggest(9999, null, 50));
    }

    [Fact]
    public void Opening_template_reports_shape_number_and_duplicate_errors()
    {
        Assert.Contains(OpeningTemplate.Parse("item_code,quantity\nI-1,1").Errors, e => e.Message.Contains("warehouse_code"));
        var csv = OpeningTemplate.Header + "\n" +
                  "I-1,W1,1,SN-1,B-1,,,جيد,2024-01-15,1500\n" +
                  "I-1,W1,1,SN-1,B-2,,,,,\n" +            // duplicate serial
                  "I-2,W1,abc,,,,,,,\n" +                  // bad quantity
                  "I-1,W1,1,SN-3,B-1,,,,,\n" +            // duplicate barcode
                  "I-1,W1,1,SN-4,,,,مكسور,,\n" +          // unknown condition
                  "\"I-2\",\"W1\",\"12.5\",,,,,,,\n";
        var (rows, errors) = OpeningTemplate.Parse("﻿" + csv);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2024, 1, 15), rows[0].PurchaseDate);
        Assert.Equal(12.5m, rows[1].Quantity);
        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, e => e.Line == 3 && e.Message.Contains("مكرر"));
        Assert.Contains(errors, e => e.Line == 5 && e.Message.Contains("باركود"));
    }

    [Fact]
    public void Document_header_rules_per_type()
    {
        DocumentRules.Header H(DocumentType t, long? w = 1, long? tw = null, long? e = null, long? u = null, string? s = null) => new(t, w, tw, e, u, s, "بيان");
        Assert.NotNull(DocumentRules.Validate(H(DocumentType.RCV)));                       // supplier missing
        Assert.Null(DocumentRules.Validate(H(DocumentType.RCV, s: "مورد")));
        Assert.NotNull(DocumentRules.Validate(H(DocumentType.ISS)));                       // no recipient
        Assert.NotNull(DocumentRules.Validate(H(DocumentType.MOV, 1, 1)));                 // same warehouse
        Assert.NotNull(DocumentRules.Validate(H(DocumentType.TRF, null, null, 1, 2)));     // both holders
        Assert.False(DocumentRules.IsManual(DocumentType.ADJ));
    }

    // ================= documents =================

    [Fact]
    public async Task Receipt_creates_numbered_assets_and_stock_equals_sum_of_movements()
    {
        var f = NewDb();
        var doc = await ReceiveAsync(f);
        Assert.Equal("Executed", doc.Status);
        Assert.StartsWith("SUP-RCV-", doc.Number);
        var assets = await f.Db.Assets.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, assets.Count);
        Assert.Equal(["AST-00001", "AST-00002"], assets.Select(a => a.AssetNumber));
        Assert.All(assets, a => Assert.Equal((AssetStatus.InStock, (long?)W1), (a.Status, a.WarehouseId)));
        Assert.Equal(new DateOnly(2027, 11, 18), assets[0].WarrantyEndDate);
        Assert.Equal(2, await Balance(f.Db, f.Laptop.Id, W1));
        Assert.Equal(20, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(4, await f.Db.SupplyDocumentLines.CountAsync(l => l.DocumentId == doc.Id && (l.AssetId != null || l.ItemId != f.Laptop.Id)));
    }

    [Fact]
    public async Task Issue_of_more_than_on_hand_rolls_back_and_issue_creates_custody()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var docs = Documents(f.Db);
        var tooMuch = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Paper.Id, 25)], unit: Unit))).Value!;
        await docs.SubmitAsync(tooMuch.Id);
        var ex = await Assert.ThrowsAsync<DomainException>(() => ApproveAsync(f.Db, tooMuch.Id));
        Assert.Contains("الرصيد لا يكفي", ex.Message);

        f.Db.ChangeTracker.Clear();
        var laptop = await f.Db.Assets.FirstAsync();
        var ok = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Laptop.Id, 1, laptop.Id), L(f.Paper.Id, 16)], employee: f.Staff.Id, unit: Unit))).Value!;
        await docs.SubmitAsync(ok.Id);
        await ApproveAsync(f.Db, ok.Id);
        var a = await f.Db.Assets.FirstAsync(x => x.Id == laptop.Id);
        Assert.Equal((AssetStatus.InCustody, (long?)null), (a.Status, a.WarehouseId));
        var c = await f.Db.Custodies.SingleAsync();
        Assert.Equal((CustodyHolder.Employee, (long?)f.Staff.Id, (long?)Unit), (c.HolderType, c.EmployeeId, c.HolderOrgUnitId));
        Assert.Equal(4, await Balance(f.Db, f.Paper.Id, W1));
        // Paper fell to 4 ≤ reorder level 5: the keeper (user 5) is told.
        Assert.True(await f.Db.Notifications.AnyAsync(n => n.RecipientUserId == 5 && n.Title == "مخزون حرج"));
        // Consumption is recorded on the department.
        Assert.Equal(-16, await f.Db.StockMovements.Where(m => m.OrgUnitId == Unit && m.ItemId == f.Paper.Id).SumAsync(m => m.Quantity));
    }

    [Fact]
    public async Task Keeper_acts_only_on_his_own_warehouse()
    {
        var f = NewDb();
        var keeperDocs = Documents(f.Db, new Scope(false, "SupplyDocuments.Create"));
        var other = await keeperDocs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 5)], wh: W2, supplier: "مورد"));
        Assert.False(other.Succeeded);
        Assert.Contains("خارج نطاق", other.Error);
        Assert.True((await keeperDocs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 5)], wh: W1, supplier: "مورد"))).Succeeded);
    }

    [Fact]
    public async Task Transfer_between_warehouses_moves_only_after_the_receiving_keeper_confirms()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        f.Db.Warehouses.Find(W2)!.KeeperEmployeeId = f.Keeper.Id;
        await f.Db.SaveChangesAsync();
        var docs = Documents(f.Db);
        var mov = (await docs.CreateAsync(Doc(DocumentType.MOV, [L(f.Paper.Id, 6)], wh: W1, target: W2))).Value!;
        await docs.SubmitAsync(mov.Id);
        var approved = await ApproveAsync(f.Db, mov.Id);
        Assert.Equal("AwaitingReceipt", approved.Status);
        Assert.Equal(20, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(0, await Balance(f.Db, f.Paper.Id, W2));

        var confirmed = await docs.ConfirmReceiptAsync(mov.Id);
        Assert.True(confirmed.Succeeded, confirmed.Error);
        Assert.Equal("Executed", confirmed.Value!.Status);
        Assert.Equal(14, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(6, await Balance(f.Db, f.Paper.Id, W2));
    }

    [Fact]
    public async Task Damaged_return_goes_to_disposal_and_disposal_keeps_the_record()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var docs = Documents(f.Db);
        var laptop = await f.Db.Assets.FirstAsync();
        var iss = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Laptop.Id, 1, laptop.Id)], employee: f.Staff.Id))).Value!;
        await docs.SubmitAsync(iss.Id);
        await ApproveAsync(f.Db, iss.Id);

        var ret = (await docs.CreateAsync(Doc(DocumentType.RET, [L(f.Laptop.Id, 1, laptop.Id, condition: "Damaged")]))).Value!;
        await docs.SubmitAsync(ret.Id);
        await ApproveAsync(f.Db, ret.Id);
        var a = await f.Db.Assets.AsNoTracking().FirstAsync(x => x.Id == laptop.Id);
        Assert.Equal((AssetStatus.PendingDisposal, (long?)W1), (a.Status, a.WarehouseId));
        Assert.NotNull((await f.Db.Custodies.SingleAsync()).ToDate);

        var dsp = (await docs.CreateAsync(Doc(DocumentType.DSP, [L(f.Laptop.Id, 1, laptop.Id)]))).Value!;
        await docs.SubmitAsync(dsp.Id);
        await ApproveAsync(f.Db, dsp.Id);
        a = await f.Db.Assets.AsNoTracking().FirstAsync(x => x.Id == laptop.Id);
        Assert.Equal(AssetStatus.Disposed, a.Status);   // never deleted (§6.5)
        Assert.Equal(1, await Balance(f.Db, f.Laptop.Id, W1));
    }

    [Fact]
    public async Task Return_needing_maintenance_opens_a_work_order()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var docs = Documents(f.Db);
        var laptop = await f.Db.Assets.FirstAsync();
        var iss = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Laptop.Id, 1, laptop.Id)], unit: Unit))).Value!;
        await docs.SubmitAsync(iss.Id);
        await ApproveAsync(f.Db, iss.Id);
        Assert.Equal(CustodyHolder.OrgUnit, (await f.Db.Custodies.SingleAsync()).HolderType);
        var ret = (await docs.CreateAsync(Doc(DocumentType.RET, [L(f.Laptop.Id, 1, laptop.Id, condition: "NeedsMaintenance")]))).Value!;
        await docs.SubmitAsync(ret.Id);
        await ApproveAsync(f.Db, ret.Id);
        var wo = await f.Db.WorkOrders.SingleAsync();
        Assert.Equal((laptop.Id, WorkOrderStatus.New), (wo.AssetId!.Value, wo.Status));
        Assert.Equal(AssetStatus.Maintenance, (await f.Db.Assets.AsNoTracking().FirstAsync(x => x.Id == laptop.Id)).Status);
    }

    [Fact]
    public async Task Submitting_without_an_approval_path_is_refused()
    {
        var f = NewDb();
        f.Db.SupplySettings.Remove(await f.Db.SupplySettings.FirstAsync(s => s.Key == "workflow.RCV"));
        await f.Db.SaveChangesAsync();
        var docs = Documents(f.Db);
        var d = (await docs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 5)], supplier: "مورد"))).Value!;
        var r = await docs.SubmitAsync(d.Id);
        Assert.False(r.Succeeded);
        Assert.Contains("مسار اعتماد", r.Error);
    }

    // ================= issue requests =================

    [Fact]
    public async Task Issue_request_partial_fulfilment_then_rest_pending()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var svc = new IssueRequestService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        var req = (await svc.CreateAsync(new SaveIssueRequest(null, false, W1, "موظف جديد",
            [new SaveIssueRequestLine(f.Laptop.Id, 1), new SaveIssueRequestLine(f.Paper.Id, 30)]))).Value!;
        Assert.Equal("Submitted", (await svc.SubmitAsync(req.Id)).Value!.Status);
        await new IssueRequestEffectHandler(f.Db, new NoopAuditWriter())
            .OnApprovedAsync(new ApprovalRequest { SourceId = req.Id, RequestType = IssueRequestService.RequestType }, default);
        req = (await svc.GetAsync(req.Id)).Value!;
        Assert.Equal("Approved", req.Status);
        Assert.Equal(20, req.Lines.Single(l => l.ItemId == f.Paper.Id).Available);

        var laptop = await f.Db.Assets.FirstAsync();
        var lapLine = req.Lines.Single(l => l.ItemId == f.Laptop.Id);
        var paperLine = req.Lines.Single(l => l.ItemId == f.Paper.Id);
        var noPiece = await svc.FulfilAsync(req.Id, new FulfilIssueRequest([new FulfilLine(lapLine.Id, 1, null)], "مستلم", req.RowVersion));
        Assert.False(noPiece.Succeeded);   // an asset needs its barcode scanned
        req = (await svc.GetAsync(req.Id)).Value!;
        var done = await svc.FulfilAsync(req.Id, new FulfilIssueRequest(
            [new FulfilLine(lapLine.Id, 1, [laptop.Id]), new FulfilLine(paperLine.Id, 20, null)], "مستلم", req.RowVersion));
        Assert.True(done.Succeeded, done.Error);
        Assert.Equal("PartiallyIssued", done.Value!.Status);
        Assert.Single(done.Value.DocumentIds);
        Assert.Equal(0, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(AssetStatus.InCustody, (await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptop.Id)).Status);

        var suspended = await svc.SuspendAsync(req.Id, "نفاد الورق");
        Assert.Equal("Suspended", suspended.Value!.Status);
    }

    [Fact]
    public async Task Monthly_quota_creates_one_request_per_department_per_month()
    {
        var f = NewDb();
        f.Db.MonthlyQuotas.Add(new MonthlyQuota { OrgUnitId = Unit, ItemId = f.Paper.Id, WarehouseId = W1, Quantity = 4 });
        await f.Db.SaveChangesAsync();
        var svc = new IssueRequestService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        Assert.Equal(new QuotaRunResult(1, 0), (await svc.RunQuotasAsync(2026, 12)).Value);
        Assert.Equal(new QuotaRunResult(0, 1), (await svc.RunQuotasAsync(2026, 12)).Value);
        var r = await f.Db.IssueRequests.Include(x => x.Lines).SingleAsync();
        Assert.Equal((IssueRequestStatus.Submitted, true, 4m), (r.Status, r.ForDepartment, r.Lines.Single().Quantity));
    }

    // ================= counts =================

    [Fact]
    public async Task Blind_count_hides_expected_then_settles_by_an_approved_adjustment()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var counts = new SupplyCountService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        var cycle = (await counts.CreateAsync(new SaveCountCycleRequest("جرد تجريبي", "Warehouse", W1, null, null,
            new DateOnly(2026, 11, 20), true, [f.Keeper.Id]))).Value!;
        Assert.StartsWith("CNT-", cycle.Number);
        await counts.StartAsync(cycle.Id);

        // Freeze: no issue on the warehouse while counting.
        var docs = Documents(f.Db);
        var iss = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Paper.Id, 1)], unit: Unit))).Value!;
        await docs.SubmitAsync(iss.Id);
        Assert.Contains("مجمّدة", (await Assert.ThrowsAsync<DomainException>(() => ApproveAsync(f.Db, iss.Id))).Message);
        f.Db.ChangeTracker.Clear();

        var sheet = (await counts.SheetAsync(cycle.Id)).Value!;
        Assert.Contains(sheet, l => l.ItemId == f.Paper.Id && l.WarehouseId == W1);
        Assert.False((await counts.VariancesAsync(cycle.Id)).Succeeded);   // blind until counting ends

        var a1 = await f.Db.Assets.OrderBy(a => a.Id).FirstAsync();
        await counts.EnterAsync(cycle.Id, new SaveCountEntry(f.Paper.Id, W1, 17, null, null, null));
        await counts.EnterAsync(cycle.Id, new SaveCountEntry(null, null, null, a1.Barcode, "Good", null));
        await counts.EnterAsync(cycle.Id, new SaveCountEntry(f.Fuse.Id, W1, 10, null, null, null));

        var variances = (await counts.ReconcileAsync(cycle.Id)).Value!;
        Assert.Contains(variances, v => v is { Kind: "Shortage", Expected: 20, Counted: 17 });
        Assert.Contains(variances, v => v.Kind == "Missing");
        Assert.Equal(2, variances.Count);
        Assert.False((await counts.SettleAsync(cycle.Id)).Succeeded);   // justification first
        foreach (var v in variances) await counts.JustifyAsync(cycle.Id, v.Id, "تبرير تجريبي");
        var settling = (await counts.SettleAsync(cycle.Id)).Value!;
        Assert.Equal("Settling", settling.Status);

        await ApproveAsync(f.Db, settling.AdjustmentDocumentId!.Value);
        Assert.Equal(17, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(1, await Balance(f.Db, f.Laptop.Id, W1));
        Assert.Equal(AssetStatus.Missing, (await f.Db.Assets.AsNoTracking().OrderBy(a => a.Id).Skip(1).FirstAsync()).Status);
        Assert.Equal("Closed", (await counts.GetAsync(cycle.Id)).Value!.Status);
        Assert.NotNull((await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == a1.Id)).LastCountedDate);
    }

    // ================= maintenance =================

    [Fact]
    public async Task Work_order_parts_come_from_the_maintenance_store_and_cost_is_tracked()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        // Move parts into the maintenance store first.
        f.Db.Warehouses.Find(WM)!.KeeperEmployeeId = f.Keeper.Id;
        await f.Db.SaveChangesAsync();
        var docs = Documents(f.Db);
        var mov = (await docs.CreateAsync(Doc(DocumentType.MOV, [L(f.Fuse.Id, 10)], wh: W1, target: WM))).Value!;
        await docs.SubmitAsync(mov.Id);
        await ApproveAsync(f.Db, mov.Id);
        Assert.True((await docs.ConfirmReceiptAsync(mov.Id)).Succeeded);

        var svc = new MaintenanceService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), Numbering(f.Db));
        var laptop = await f.Db.Assets.FirstAsync();
        var wo = (await svc.ReportAsync(new ReportFaultRequest(laptop.Barcode, null, null, "لا يعمل"))).Value!;
        Assert.StartsWith("WO-", wo.Number);
        Assert.Equal(AssetStatus.Maintenance, (await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptop.Id)).Status);
        Assert.False((await svc.ReportAsync(new ReportFaultRequest(laptop.Barcode, null, null, "مرة ثانية"))).Succeeded);

        wo = (await svc.UpdateAsync(wo.Id, new UpdateWorkOrderRequest(f.Keeper.Id, "InProgress", "تقييم", wo.RowVersion))).Value!;
        Assert.False((await svc.IssuePartsAsync(wo.Id, new WorkOrderPartsRequest(W1, [new(f.Fuse.Id, 1)], wo.RowVersion))).Succeeded);
        var withParts = await svc.IssuePartsAsync(wo.Id, new WorkOrderPartsRequest(WM, [new(f.Fuse.Id, 3)], wo.RowVersion));
        Assert.True(withParts.Succeeded, withParts.Error);
        Assert.Equal(6m, withParts.Value!.PartsCost);   // 3 × 2.00 receipt value
        Assert.Equal(7, await Balance(f.Db, f.Fuse.Id, WM));

        var closed = await svc.CloseAsync(wo.Id, new CloseWorkOrderRequest(true, 1.5m, 40, "استُبدلت القطعة", withParts.Value.RowVersion));
        Assert.Equal("Closed", closed.Value!.Status);
        Assert.Equal(AssetStatus.InStock, (await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptop.Id)).Status);
    }

    // ================= opening =================

    [Fact]
    public async Task Opening_import_reports_errors_then_posts_balances_and_custody_and_locks()
    {
        var f = NewDb();
        var svc = new OpeningBalanceService(f.Db, new NoopAuditWriter());
        var bad = await svc.UploadAsync(new UploadOpeningRequest("o.csv", "دائرة أ", OpeningTemplate.Header + "\nI-1,,1,SN-9,,T-404,,,,\nI-X,W1,1,,,,,,,\n"));
        Assert.Equal("Failed", bad.Value!.Status);
        Assert.Contains(bad.Value.Errors, e => e.Message.Contains("موظف غير موجود"));
        Assert.Contains(bad.Value.Errors, e => e.Message.Contains("صنف غير معرّف"));

        var good = await svc.UploadAsync(new UploadOpeningRequest("o.csv", "دائرة أ", OpeningTemplate.Header +
            "\nI-1,,1,SN-9,LBL-1,T-2,,جيد,2023-05-01,2000\nI-1,W1,1,SN-8,,,,تالف,,\nI-2,W1,12,,,,,,,\n"));
        Assert.Equal("DocumentCreated", good.Value!.Status);
        var docs = Documents(f.Db);
        Assert.True((await docs.SubmitAsync(good.Value.DocumentId!.Value)).Succeeded);
        await ApproveAsync(f.Db, good.Value.DocumentId!.Value);

        var held = await f.Db.Assets.SingleAsync(a => a.SerialNumber == "SN-9");
        Assert.Equal(("LBL-1", AssetStatus.InCustody), (held.Barcode, held.Status));
        Assert.Equal(f.Staff.Id, (await f.Db.Custodies.SingleAsync(c => c.AssetId == held.Id)).EmployeeId);
        Assert.Equal(AssetStatus.PendingDisposal, (await f.Db.Assets.SingleAsync(a => a.SerialNumber == "SN-8")).Status);
        Assert.Equal(12, await Balance(f.Db, f.Paper.Id, W1));

        Assert.True((await svc.LockAsync()).Succeeded);
        Assert.False((await svc.UploadAsync(new UploadOpeningRequest("o.csv", "مرحلة", OpeningTemplate.Header + "\nI-2,W1,1,,,,,,,\n"))).Succeeded);
    }

    // ================= endpoints & roles =================

    private static readonly Type[] Controllers =
    [
        typeof(SupplyCatalogController), typeof(SupplyStockController), typeof(SupplyDocumentsController), typeof(SupplyRequestsController),
        typeof(SupplyCountsController), typeof(SupplyMaintenanceController), typeof(SupplyOpeningController),
    ];

    [Fact]
    public void Every_supply_endpoint_declares_a_known_permission_and_writes_are_not_view_only()
    {
        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attr = action.GetCustomAttribute<HasPermissionAttribute>();
            Assert.True(attr is not null, $"{controller.Name}.{action.Name} has no permission");
            var code = attr!.Policy![HasPermissionAttribute.PolicyPrefix.Length..];
            Assert.Contains(code, PermissionCatalog.AllCodes);
            var verb = action.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault()?.HttpMethods.Single();
            // "printed" only records that a document was printed (audit); it changes nothing.
            if (verb is null or "GET" || action.Name == "Printed") continue;
            Assert.False(code.EndsWith(".View"), $"{controller.Name}.{action.Name} ({verb}) is guarded only by a View permission");
        }
    }

    [Fact]
    public void Every_role_may_request_supplies_but_only_the_supply_manager_sees_all_warehouses()
    {
        Permission P(string r, PermissionAction a) => new() { Resource = r, Action = a };
        foreach (var role in PermissionCatalog.RoleDefaults)
            Assert.True(role.Pick(P("SupplyRequests", PermissionAction.Create)), role.Name);
        Assert.False(PermissionCatalog.RoleDefaults.Single(r => r.Name == "Manager").Pick(P("SupplyAdmin", PermissionAction.View)));
        Assert.False(PermissionCatalog.RoleDefaults.Single(r => r.Name == "Warehouse Keeper").Pick(P("SupplyAdmin", PermissionAction.View)));
        Assert.True(PermissionCatalog.RoleDefaults.Single(r => r.Name == "Supply Manager").Pick(P("SupplyAdmin", PermissionAction.View)));
    }
}
