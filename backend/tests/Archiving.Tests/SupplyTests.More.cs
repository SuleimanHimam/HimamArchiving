using System.Reflection;
using System.Security.Claims;
using Archiving.Api.Authorization;
using Archiving.Application.Features.Supply;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Supply;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Supply;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DocumentType = Archiving.Domain.Supply.DocumentType;

namespace Archiving.Tests;

/// <summary>Second half of the supply tests: custody transfer, decisions, alerts, reports, unit-scope counts,
/// unrepaired assets, catalogue rules, permission enforcement and the real approval outcome path.</summary>
public partial class SupplyTests
{
    private static async Task<long> IssueAsync(Fixture f, long assetId, long? employee = null, long? unit = null)
    {
        var docs = Documents(f.Db);
        var d = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Laptop.Id, 1, assetId)], employee: employee, unit: unit))).Value!;
        await docs.SubmitAsync(d.Id);
        await ApproveAsync(f.Db, d.Id);
        return d.Id;
    }

    private static SupplyStockService Stock(AppDbContext db) => new(db, new Scope(), new TestCurrentUser(), new NoopAuditWriter());

    // ---- 1. custody transfer ----

    [Fact]
    public async Task Custody_transfer_closes_the_old_holder_and_opens_the_new_one()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var laptop = await f.Db.Assets.FirstAsync();
        await IssueAsync(f, laptop.Id, employee: f.Staff.Id);

        var docs = Documents(f.Db);
        var trf = (await docs.CreateAsync(Doc(DocumentType.TRF, [L(f.Laptop.Id, 1, laptop.Id)], wh: null, unit: Unit))).Value!;
        await docs.SubmitAsync(trf.Id);
        await ApproveAsync(f.Db, trf.Id);

        var all = await f.Db.Custodies.OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal((trf.Id, (long?)f.Staff.Id), (all[0].ClosedByDocumentId!.Value, all[0].EmployeeId));
        Assert.NotNull(all[0].ToDate);
        Assert.Equal((CustodyHolder.OrgUnit, (long?)Unit, (DateOnly?)null), (all[1].HolderType, all[1].OrgUnitId, all[1].ToDate));
        Assert.Equal(AssetStatus.InCustody, (await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptop.Id)).Status);
        // Stock is untouched by a custody transfer.
        Assert.Equal(1, await Balance(f.Db, f.Laptop.Id, W1));
    }

    [Fact]
    public async Task Custody_transfer_refuses_a_piece_that_is_in_the_store()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var laptop = await f.Db.Assets.FirstAsync();
        var docs = Documents(f.Db);
        var trf = (await docs.CreateAsync(Doc(DocumentType.TRF, [L(f.Laptop.Id, 1, laptop.Id)], wh: null, employee: f.Staff.Id))).Value!;
        await docs.SubmitAsync(trf.Id);
        Assert.Contains("ليس في عهدة", (await Assert.ThrowsAsync<DomainException>(() => ApproveAsync(f.Db, trf.Id))).Message);
    }

    // ---- 2. rejection and return ----

    [Fact]
    public async Task Rejected_document_keeps_its_reason_moves_nothing_and_tells_the_creator()
    {
        var f = NewDb();
        var docs = Documents(f.Db);
        var d = (await docs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 9)], supplier: "مورد"))).Value!;
        await docs.SubmitAsync(d.Id);
        await DocEffect(f.Db).OnRejectedAsync(new ApprovalRequest { SourceId = d.Id, RequestType = SupplyDocumentService.RequestType }, "الفاتورة غير مطابقة", default);

        var after = (await docs.GetAsync(d.Id)).Value!;
        Assert.Equal(("Rejected", "الفاتورة غير مطابقة"), (after.Status, after.DecisionNote));
        Assert.Equal(0, await Balance(f.Db, f.Paper.Id, W1));
        Assert.True(await f.Db.Notifications.AnyAsync(n => n.RecipientUserId == 5 && n.Title == "رُفض المحضر"));
    }

    [Fact]
    public async Task Returned_document_can_be_corrected_and_submitted_again()
    {
        var f = NewDb();
        var docs = Documents(f.Db);
        var d = (await docs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 9)], supplier: "مورد"))).Value!;
        await docs.SubmitAsync(d.Id);
        await DocEffect(f.Db).OnReturnedAsync(new ApprovalRequest { SourceId = d.Id, RequestType = SupplyDocumentService.RequestType }, "صحّح الكمية", default);
        var returned = (await docs.GetAsync(d.Id)).Value!;
        Assert.Equal("Returned", returned.Status);

        var fixedDoc = await docs.UpdateAsync(d.Id, Doc(DocumentType.RCV, [L(f.Paper.Id, 10)], supplier: "مورد") with { RowVersion = returned.RowVersion });
        Assert.True(fixedDoc.Succeeded, fixedDoc.Error);
        Assert.Equal("Submitted", (await docs.SubmitAsync(d.Id)).Value!.Status);
        Assert.Equal(returned.Number, (await docs.GetAsync(d.Id)).Value!.Number);   // the number is kept, no gaps
    }

    [Fact]
    public async Task Refused_count_settlement_sends_the_cycle_back_to_justification()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var counts = new SupplyCountService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        var c = (await counts.CreateAsync(new SaveCountCycleRequest("جرد", "Warehouse", W1, null, null, new DateOnly(2026, 11, 20), false, [f.Keeper.Id]))).Value!;
        await counts.StartAsync(c.Id);
        await counts.EnterAsync(c.Id, new SaveCountEntry(f.Paper.Id, W1, 19, null, null, null));
        foreach (var v in (await counts.ReconcileAsync(c.Id)).Value!) await counts.JustifyAsync(c.Id, v.Id, "تبرير");
        var settling = (await counts.SettleAsync(c.Id)).Value!;

        await DocEffect(f.Db).OnRejectedAsync(new ApprovalRequest { SourceId = settling.AdjustmentDocumentId!.Value, RequestType = SupplyDocumentService.RequestType }, "أعيدوا العدّ", default);
        var back = (await counts.GetAsync(c.Id)).Value!;
        Assert.Equal(("Reconciled", (long?)null), (back.Status, back.AdjustmentDocumentId));
        Assert.Equal(20, await Balance(f.Db, f.Paper.Id, W1));   // nothing booked
    }

    // ---- 3. alerts ----

    [Fact]
    public async Task Dashboard_raises_pending_custody_warranty_not_counted_and_suspended_alerts()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var laptops = await f.Db.Assets.OrderBy(a => a.Id).ToListAsync();
        await IssueAsync(f, laptops[0].Id, employee: f.Staff.Id);

        // The holder's service ends while the laptop is still in their custody.
        f.Db.Employees.Find(f.Staff.Id)!.Status = EmployeeStatus.Terminated;
        // The other laptop's warranty ends in ten days; the alert window is 30.
        laptops[1].WarrantyEndDate = SupplyCommon.Today().AddDays(10);
        f.Db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.WarrantyAlertDays, Value = "30" });
        f.Db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.SuspendedRequestDays, Value = "7" });
        f.Db.IssueRequests.Add(new IssueRequest
        {
            Number = "SR-T-1", RequesterEmployeeId = f.Keeper.Id, OrgUnitId = Unit, WarehouseId = W1, Status = IssueRequestStatus.Suspended,
            SuspendedAt = DateTime.UtcNow.AddDays(-10),
        });
        await f.Db.SaveChangesAsync();

        var dash = await Stock(f.Db).DashboardAsync();
        Assert.Contains(dash.Alerts, a => a.Kind == "PendingCustody" && a.Title == f.Staff.FullName);
        Assert.Contains(dash.Alerts, a => a.Kind == "Warranty" && a.Title.StartsWith(laptops[1].AssetNumber));
        Assert.Contains(dash.Alerts, a => a.Kind == "NotCounted");          // pieces received, never counted
        Assert.Contains(dash.Alerts, a => a.Kind == "SuspendedRequest" && a.Title == "SR-T-1");
        Assert.Equal(1, dash.AssetsInCustody);
    }

    [Fact]
    public async Task Pending_custody_is_flagged_when_the_holder_moves_to_another_unit()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        await IssueAsync(f, (await f.Db.Assets.FirstAsync()).Id, employee: f.Staff.Id);
        Assert.DoesNotContain((await Stock(f.Db).DashboardAsync()).Alerts, a => a.Kind == "PendingCustody");

        f.Db.OrgUnits.Add(new OrgUnit { Id = 11, InstitutionId = 1, Name = "دائرة ب" });
        f.Db.Employees.Find(f.Staff.Id)!.OrgUnitId = 11;
        await f.Db.SaveChangesAsync();
        Assert.Contains((await Stock(f.Db).DashboardAsync()).Alerts, a => a.Kind == "PendingCustody");
    }

    // ---- 4. reports ----

    [Fact]
    public async Task Item_card_carries_the_opening_and_a_running_balance()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var today = SupplyCommon.Today();
        // Move the receipt a month back: it becomes the opening balance of this period.
        foreach (var m in f.Db.StockMovements.Where(m => m.ItemId == f.Paper.Id)) m.MovementDate = today.AddDays(-30);
        await f.Db.SaveChangesAsync();
        var docs = Documents(f.Db);
        var iss = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Paper.Id, 6)], unit: Unit))).Value!;
        await docs.SubmitAsync(iss.Id);
        await ApproveAsync(f.Db, iss.Id);

        var card = (await Stock(f.Db).ItemCardAsync(f.Paper.Id, W1, today.AddDays(-7), today)).Value!;
        Assert.Equal((20m, 14m), (card.Opening, card.Closing));
        var line = Assert.Single(card.Lines);
        Assert.Equal((0m, 6m, 14m, "ISS"), (line.In, line.Out, line.Balance, line.DocumentType));
        Assert.False((await Stock(f.Db).ItemCardAsync(f.Paper.Id, null, today, today.AddDays(-1))).Succeeded);
    }

    [Fact]
    public async Task Consumption_report_compares_with_the_previous_period_and_stagnant_items_are_listed()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var today = SupplyCommon.Today();
        var docs = Documents(f.Db);
        foreach (var qty in new[] { 4m, 3m })
        {
            var d = (await docs.CreateAsync(Doc(DocumentType.ISS, [L(f.Paper.Id, qty)], unit: Unit))).Value!;
            await docs.SubmitAsync(d.Id);
            await ApproveAsync(f.Db, d.Id);
        }
        // The first issue falls in the previous period of the same length.
        var first = await f.Db.StockMovements.Where(m => m.ItemId == f.Paper.Id && m.Quantity == -4).SingleAsync();
        first.MovementDate = today.AddDays(-15);
        // Maintenance parts last moved long ago: stagnant after 30 days.
        foreach (var m in f.Db.StockMovements.Where(m => m.ItemId == f.Fuse.Id)) m.MovementDate = today.AddDays(-60);
        f.Db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.StagnantDays, Value = "30" });
        await f.Db.SaveChangesAsync();

        var row = Assert.Single(await Stock(f.Db).ConsumptionReportAsync(today.AddDays(-9), today));
        Assert.Equal((Unit, 3m, 4m), (row.OrgUnitId, row.Quantity, row.PreviousQuantity));
        var stagnant = await Stock(f.Db).StagnantReportAsync();
        Assert.Contains(stagnant, s => s.ItemId == f.Fuse.Id && s.Balance == 10);
        Assert.DoesNotContain(stagnant, s => s.ItemId == f.Paper.Id);
    }

    // ---- 5. department-scope count ----

    [Fact]
    public async Task Department_count_checks_its_custody_finds_missing_and_misplaced_pieces()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var laptops = await f.Db.Assets.OrderBy(a => a.Id).ToListAsync();
        await IssueAsync(f, laptops[0].Id, employee: f.Staff.Id);    // personal custody of a unit member
        await IssueAsync(f, laptops[1].Id, unit: Unit);              // departmental custody
        var more = (await Documents(f.Db).CreateAsync(Doc(DocumentType.RCV, [L(f.Laptop.Id, 1, serial: "SN-C")], supplier: "مورد"))).Value!;
        await Documents(f.Db).SubmitAsync(more.Id);
        await ApproveAsync(f.Db, more.Id);                          // a third laptop stays in the store
        var counts = new SupplyCountService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        var c = (await counts.CreateAsync(new SaveCountCycleRequest("جرد عهد الدائرة", "OrgUnit", null, Unit, null, new DateOnly(2026, 11, 20), true, [f.Keeper.Id]))).Value!;
        await counts.StartAsync(c.Id);

        // A unit-scope freeze blocks returning a piece held by that unit.
        var docs = Documents(f.Db);
        var ret = (await docs.CreateAsync(Doc(DocumentType.RET, [L(f.Laptop.Id, 1, laptops[1].Id)]))).Value!;
        await docs.SubmitAsync(ret.Id);
        Assert.Contains("مجمّدة", (await Assert.ThrowsAsync<DomainException>(() => ApproveAsync(f.Db, ret.Id))).Message);
        f.Db.ChangeTracker.Clear();

        // The committee finds the personal laptop, not the departmental one — and the store's own laptop on a desk.
        await counts.EnterAsync(c.Id, new SaveCountEntry(null, null, null, laptops[0].Barcode, "Good", null));
        var stray = await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Status == AssetStatus.InStock && a.ItemId == f.Laptop.Id);
        await counts.EnterAsync(c.Id, new SaveCountEntry(null, null, null, stray.Barcode, "Good", null));
        var v = (await counts.ReconcileAsync(c.Id)).Value!;
        Assert.Contains(v, x => x.Kind == "Missing" && x.AssetId == laptops[1].Id);
        Assert.Contains(v, x => x.Kind == "Misplaced" && x.AssetId == stray.Id);
        Assert.Equal(2, v.Count);
    }

    // ---- 6. maintenance that fails ----

    [Fact]
    public async Task Unrepaired_asset_waits_for_disposal_and_costly_repairs_suggest_replacement()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        f.Db.SupplySettings.Add(new SupplySetting { Key = SupplySettingKeys.ReplacementCostPercent, Value = "50" });
        await f.Db.SaveChangesAsync();
        var laptops = await f.Db.Assets.OrderBy(a => a.Id).ToListAsync();
        await IssueAsync(f, laptops[0].Id, employee: f.Staff.Id);
        var svc = new MaintenanceService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), Numbering(f.Db));

        // In custody, not repairable: stays with the holder, damaged, until a disposal document.
        var held = (await svc.ReportAsync(new ReportFaultRequest(laptops[0].Barcode, null, null, "شاشة مكسورة"))).Value!;
        await svc.CloseAsync(held.Id, new CloseWorkOrderRequest(false, 2, 100, "لا قطع بديلة", held.RowVersion));
        var a0 = await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptops[0].Id);
        Assert.Equal((AssetStatus.InCustody, AssetCondition.Damaged), (a0.Status, a0.Condition));
        Assert.Equal("RecommendedDisposal", (await svc.GetAsync(held.Id)).Value!.Status);

        // In the store: 2,000 labour on a 3,000 piece passes 50 % → replacement suggested; unrepaired → for disposal.
        var stored = (await svc.ReportAsync(new ReportFaultRequest(laptops[1].Barcode, null, null, "لا يقلع"))).Value!;
        var closed = (await svc.CloseAsync(stored.Id, new CloseWorkOrderRequest(false, 10, 2000, "اللوحة الأم تالفة", stored.RowVersion))).Value!;
        Assert.True(closed.SuggestReplacement);
        Assert.Equal(AssetStatus.PendingDisposal, (await f.Db.Assets.AsNoTracking().FirstAsync(a => a.Id == laptops[1].Id)).Status);
        var card = (await Stock(f.Db).AssetCardAsync(laptops[1].Id)).Value!;
        Assert.Equal((1, 2000m, true), (card.FaultCount, card.MaintenanceCost, card.SuggestReplacement));
        Assert.Contains(card.History, h => h.Kind == "WO");
    }

    [Fact]
    public async Task Fault_needs_an_asset_or_a_place_and_a_known_barcode()
    {
        var f = NewDb();
        var svc = new MaintenanceService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), Numbering(f.Db));
        Assert.False((await svc.ReportAsync(new ReportFaultRequest(null, null, null, "عطل"))).Succeeded);
        Assert.False((await svc.ReportAsync(new ReportFaultRequest("NO-SUCH", null, null, "عطل"))).Succeeded);
        var place = await svc.ReportAsync(new ReportFaultRequest(null, "مغسلة الطابق الأرضي", null, "تسريب مياه"));
        Assert.True(place.Succeeded, place.Error);
        Assert.Null(place.Value!.AssetId);
    }

    // ---- 7. catalogue rules ----

    [Fact]
    public async Task Catalogue_keeps_two_levels_one_kind_per_branch_and_locks_kind_once_used()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var catalog = new SupplyCatalogService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter());
        var main = (await catalog.CategoriesAsync()).First(c => c.Code == "C-C");
        var sub = (await catalog.SaveCategoryAsync(null, new SaveItemCategoryRequest(main.Id, "C-C1", "ورق", "Consumable", true))).Value!;
        Assert.False((await catalog.SaveCategoryAsync(null, new SaveItemCategoryRequest(sub.Id, "C-C2", "مستوى ثالث", "Consumable", true))).Succeeded);
        Assert.False((await catalog.SaveCategoryAsync(null, new SaveItemCategoryRequest(main.Id, "C-X", "نوع مختلف", "FixedAsset", true))).Succeeded);

        // Paper has movements: it cannot be turned into an asset by moving it to an asset category.
        var assetCat = (await catalog.CategoriesAsync()).First(c => c.Code == "C-A");
        var paper = (await catalog.ItemsAsync(false)).First(i => i.Id == f.Paper.Id);
        var moved = await catalog.SaveItemAsync(paper.Id, new SaveItemRequest(paper.Code, paper.Name, assetCat.Id, paper.Unit, null, null, null, null, true, paper.RowVersion));
        Assert.Contains("لا يتغير نوع", moved.Error);
        Assert.False((await catalog.SaveItemAsync(null, new SaveItemRequest("I-1", "مكرر", assetCat.Id, "جهاز", null, null, null, null, true))).Succeeded);
    }

    [Fact]
    public async Task Warehouse_with_stock_cannot_be_deactivated()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var catalog = new SupplyCatalogService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter());
        var w = (await catalog.WarehousesAsync()).First(x => x.Id == W1);
        var r = await catalog.SaveWarehouseAsync(W1, new SaveWarehouseRequest(w.Code, w.Name, w.Kind, null, w.KeeperEmployeeId, false, w.RowVersion));
        Assert.Contains("فيه رصيد", r.Error);
    }

    // ---- 8. permission enforcement ----

    [Fact]
    public async Task Every_supply_endpoint_rejects_a_user_without_its_permission_and_admits_one_with_it()
    {
        var services = new ServiceCollection().AddLogging().AddAuthorization()
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .BuildServiceProvider();
        var authz = services.GetRequiredService<IAuthorizationService>();
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        static ClaimsPrincipal User(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

        var checkedCount = 0;
        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var policyName = action.GetCustomAttribute<HasPermissionAttribute>()!.Policy!;
            var code = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var policy = (await provider.GetPolicyAsync(policyName))!;
            var other = code.EndsWith(".View") ? code.Replace(".View", ".Edit") : code.Split('.')[0] + ".View";

            Assert.False((await authz.AuthorizeAsync(User(), null, policy)).Succeeded, $"{action.Name}: anonymous claims passed");
            Assert.False((await authz.AuthorizeAsync(User(new Claim("permission", other)), null, policy)).Succeeded,
                $"{controller.Name}.{action.Name}: {other} should not open {code}");
            Assert.True((await authz.AuthorizeAsync(User(new Claim("permission", code)), null, policy)).Succeeded, $"{action.Name}: {code} refused");
            Assert.True((await authz.AuthorizeAsync(User(new Claim(ClaimTypes.Role, PermissionAuthorizationHandler.AdminRole)), null, policy)).Succeeded);
            checkedCount++;
        }
        Assert.True(checkedCount >= 60, $"only {checkedCount} endpoints checked");
    }

    [Fact]
    public async Task Keeper_fulfils_only_requests_on_his_warehouse_and_others_see_only_their_own_requests()
    {
        var f = NewDb();
        await ReceiveAsync(f);
        var admin = new IssueRequestService(f.Db, new Scope(), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        var req = (await admin.CreateAsync(new SaveIssueRequest(f.Staff.Id, false, W2, "طلب على مستودع ب", [new SaveIssueRequestLine(f.Paper.Id, 1)]))).Value!;
        await admin.SubmitAsync(req.Id);
        await new IssueRequestEffectHandler(f.Db, new NoopAuditWriter())
            .OnApprovedAsync(new ApprovalRequest { SourceId = req.Id, RequestType = IssueRequestService.RequestType }, default);

        // The signed-in user (5) keeps W1 only; W2 has no keeper.
        var keeper = new IssueRequestService(f.Db, new Scope(false, "SupplyRequests.View", "SupplyRequests.Edit"), new TestCurrentUser(),
            new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        req = (await admin.GetAsync(req.Id)).Value!;
        var r = await keeper.FulfilAsync(req.Id, new FulfilIssueRequest([new FulfilLine(req.Lines[0].Id, 1, null)], null, req.RowVersion));
        Assert.False(r.Succeeded);

        // Without SupplyRequests.View the list is only one's own requests.
        var plain = new IssueRequestService(f.Db, new Scope(false), new TestCurrentUser(), new NoopAuditWriter(), new StubApprovals(), Numbering(f.Db));
        Assert.Empty((await plain.ListAsync(new IssueRequestQuery(null, false))).Items);
    }

    // ---- 9. the real approval outcome path ----

    [Fact]
    public async Task Approval_outcome_handler_executes_the_document_and_refuses_a_rejection_without_reason()
    {
        var f = NewDb();
        var docs = Documents(f.Db);
        var d = (await docs.CreateAsync(Doc(DocumentType.RCV, [L(f.Paper.Id, 12)], supplier: "مورد"))).Value!;
        await docs.SubmitAsync(d.Id);
        var handler = new ApprovalOutcomeHandler(f.Db, [DocEffect(f.Db), new IssueRequestEffectHandler(f.Db, new NoopAuditWriter())]);

        var refused = new ApprovalRequest { Number = "REQ-T-1", RequestType = SupplyDocumentService.RequestType, SourceModule = "Supply", SourceId = d.Id, Title = "t" };
        f.Db.ApprovalRequests.Add(refused);
        await f.Db.SaveChangesAsync();
        Assert.Contains("سبب الرفض", (await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(refused.Id, WorkflowStatus.Rejected, " ", default))).Message);

        // An intermediate level acting changes nothing; the run completing executes the document.
        await handler.HandleAsync(refused.Id, WorkflowStatus.Running, null, default);
        Assert.Equal("Submitted", (await docs.GetAsync(d.Id)).Value!.Status);
        await handler.HandleAsync(refused.Id, WorkflowStatus.Completed, null, default);
        await f.Db.SaveChangesAsync();
        Assert.Equal("Executed", (await docs.GetAsync(d.Id)).Value!.Status);
        Assert.Equal(12, await Balance(f.Db, f.Paper.Id, W1));
        Assert.Equal(ApprovalRequestStatus.Approved, (await f.Db.ApprovalRequests.FirstAsync(a => a.Id == refused.Id)).Status);
    }
}
