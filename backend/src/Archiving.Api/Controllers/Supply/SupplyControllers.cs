using System.Text;
using Archiving.Api.Authorization;
using Archiving.Api.Controllers.Hr;
using Archiving.Application.Features.Supply;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers.Supply;

// اللوازم والمخازن. Endpoints only: permission, call, result. Warehouse scope and rules live in the services.

public sealed record ReasonBody(string Reason);
public sealed record JustifyBody(string Justification);

[ApiController]
[Route("api/supply/catalog")]
[Authorize]
public sealed class SupplyCatalogController(ISupplyCatalogService catalog) : ControllerBase
{
    [HttpGet("settings")]
    [HasPermission("SupplyItems.View")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await catalog.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveSupplySettingsRequest r, CancellationToken ct) => this.From(await catalog.SaveSettingsAsync(r, ct));

    [HttpGet("categories")]
    [HasPermission("SupplyItems.View")]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await catalog.CategoriesAsync(ct));

    [HttpPost("categories")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveItemCategoryRequest r, CancellationToken ct) => this.From(await catalog.SaveCategoryAsync(null, r, ct));

    [HttpPut("categories/{id:long}")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] SaveItemCategoryRequest r, CancellationToken ct) => this.From(await catalog.SaveCategoryAsync(id, r, ct));

    /// <summary>Active items are what a requester picks from, so the list is open to anyone who may request.</summary>
    [HttpGet("items")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Items([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await catalog.ItemsAsync(activeOnly, ct));

    [HttpPost("items")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> CreateItem([FromBody] SaveItemRequest r, CancellationToken ct) => this.From(await catalog.SaveItemAsync(null, r, ct));

    [HttpPut("items/{id:long}")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] SaveItemRequest r, CancellationToken ct) => this.From(await catalog.SaveItemAsync(id, r, ct));

    [HttpGet("warehouses")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await catalog.WarehousesAsync(ct));

    [HttpPost("warehouses")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> CreateWarehouse([FromBody] SaveWarehouseRequest r, CancellationToken ct) => this.From(await catalog.SaveWarehouseAsync(null, r, ct));

    [HttpPut("warehouses/{id:long}")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> UpdateWarehouse(long id, [FromBody] SaveWarehouseRequest r, CancellationToken ct) => this.From(await catalog.SaveWarehouseAsync(id, r, ct));

    [HttpGet("quotas")]
    [HasPermission("SupplyItems.View")]
    public async Task<IActionResult> Quotas(CancellationToken ct) => Ok(await catalog.QuotasAsync(ct));

    [HttpPost("quotas")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> CreateQuota([FromBody] SaveMonthlyQuotaRequest r, CancellationToken ct) => this.From(await catalog.SaveQuotaAsync(null, r, ct));

    [HttpPut("quotas/{id:long}")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> UpdateQuota(long id, [FromBody] SaveMonthlyQuotaRequest r, CancellationToken ct) => this.From(await catalog.SaveQuotaAsync(id, r, ct));

    [HttpDelete("quotas/{id:long}")]
    [HasPermission("SupplyItems.Edit")]
    public async Task<IActionResult> DeleteQuota(long id, CancellationToken ct) => this.Deleted(await catalog.DeleteQuotaAsync(id, ct));
}

[ApiController]
[Route("api/supply/stock")]
[Authorize]
public sealed class SupplyStockController(ISupplyStockService stock) : ControllerBase
{
    [HttpGet("dashboard")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(await stock.DashboardAsync(ct));

    [HttpGet("catalogue")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Catalogue(CancellationToken ct) => Ok(await stock.CatalogueStockAsync(ct));

    /// <summary>"المتوفر" next to each line of a request form.</summary>
    [HttpGet("available/{warehouseId:long}")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Available(long warehouseId, CancellationToken ct) => Ok(await stock.AvailableAsync(warehouseId, ct));

    [HttpGet("assets")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Assets([FromQuery] AssetQuery q, CancellationToken ct) => Ok(await stock.AssetsAsync(q, ct));

    [HttpGet("assets/by-barcode/{barcode}")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> ByBarcode(string barcode, CancellationToken ct) => this.From(await stock.FindByBarcodeAsync(barcode, ct));

    [HttpGet("assets/{id:long}")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> AssetCard(long id, CancellationToken ct) => this.From(await stock.AssetCardAsync(id, ct));

    [HttpGet("custody")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Custody([FromQuery] CustodyQuery q, CancellationToken ct) => this.From(await stock.CustodyAsync(q, ct));

    [HttpGet("custody/mine")]
    [HasPermission("SelfService.View")]
    public async Task<IActionResult> MyCustody(CancellationToken ct) => Ok(await stock.MyCustodyAsync(ct));

    /// <summary>The «العهدة» tab of the personnel file (read-only, §8.2).</summary>
    [HttpGet("custody/employee/{employeeId:long}")]
    [HasPermission("HrProfile.View")]
    public async Task<IActionResult> EmployeeCustody(long employeeId, CancellationToken ct) => Ok(await stock.EmployeeCustodyAsync(employeeId, ct));

    [HttpGet("reports/balances")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Balances([FromQuery] long? warehouseId, CancellationToken ct) => Ok(await stock.BalancesReportAsync(warehouseId, ct));

    [HttpGet("reports/item-card/{itemId:long}")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> ItemCard(long itemId, [FromQuery] long? warehouseId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        this.From(await stock.ItemCardAsync(itemId, warehouseId, from, to, ct));

    [HttpGet("reports/consumption")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Consumption([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await stock.ConsumptionReportAsync(from, to, ct));

    [HttpGet("reports/maintenance")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Maintenance([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await stock.MaintenanceReportAsync(from, to, ct));

    [HttpGet("reports/stagnant")]
    [HasPermission("SupplyStock.View")]
    public async Task<IActionResult> Stagnant(CancellationToken ct) => Ok(await stock.StagnantReportAsync(ct));
}

[ApiController]
[Route("api/supply/documents")]
[Authorize]
public sealed class SupplyDocumentsController(ISupplyDocumentService documents) : ControllerBase
{
    [HttpGet]
    [HasPermission("SupplyDocuments.View")]
    public async Task<IActionResult> List([FromQuery] DocumentQuery q, CancellationToken ct) => Ok(await documents.ListAsync(q, ct));

    [HttpGet("counts")]
    [HasPermission("SupplyDocuments.View")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await documents.CountsAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("SupplyDocuments.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await documents.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("SupplyDocuments.Create")]
    public async Task<IActionResult> Create([FromBody] SaveDocumentRequest r, CancellationToken ct) => this.From(await documents.CreateAsync(r, ct));

    [HttpPut("{id:long}")]
    [HasPermission("SupplyDocuments.Create")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveDocumentRequest r, CancellationToken ct) => this.From(await documents.UpdateAsync(id, r, ct));

    [HttpPost("{id:long}/submit")]
    [HasPermission("SupplyDocuments.Create")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) => this.From(await documents.SubmitAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("SupplyDocuments.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => this.From(await documents.CancelAsync(id, ct));

    [HttpPost("{id:long}/confirm-receipt")]
    [HasPermission("SupplyDocuments.Edit")]
    public async Task<IActionResult> ConfirmReceipt(long id, CancellationToken ct) => this.From(await documents.ConfirmReceiptAsync(id, ct));

    [HttpPost("{id:long}/printed")]
    [HasPermission("SupplyDocuments.View")]
    public async Task<IActionResult> Printed(long id, CancellationToken ct) => this.From(await documents.PrintedAsync(id, ct));
}

[ApiController]
[Route("api/supply/requests")]
[Authorize]
public sealed class SupplyRequestsController(IIssueRequestService requests) : ControllerBase
{
    /// <summary>Own requests for everyone; others' with SupplyRequests.View (scoped in the service).</summary>
    [HttpGet]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> List([FromQuery] IssueRequestQuery q, CancellationToken ct) => Ok(await requests.ListAsync(q, ct));

    [HttpGet("{id:long}")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await requests.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Create([FromBody] SaveIssueRequest r, CancellationToken ct) => this.From(await requests.CreateAsync(r, ct));

    [HttpPut("{id:long}")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveIssueRequest r, CancellationToken ct) => this.From(await requests.UpdateAsync(id, r, ct));

    [HttpPost("{id:long}/submit")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) => this.From(await requests.SubmitAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("SupplyRequests.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => this.From(await requests.CancelAsync(id, ct));

    [HttpPost("{id:long}/fulfil")]
    [HasPermission("SupplyRequests.Edit")]
    public async Task<IActionResult> Fulfil(long id, [FromBody] FulfilIssueRequest r, CancellationToken ct) => this.From(await requests.FulfilAsync(id, r, ct));

    [HttpPost("{id:long}/suspend")]
    [HasPermission("SupplyRequests.Edit")]
    public async Task<IActionResult> Suspend(long id, [FromBody] ReasonBody r, CancellationToken ct) => this.From(await requests.SuspendAsync(id, r.Reason, ct));

    [HttpPost("quotas/run")]
    [HasPermission("SupplyRequests.Edit")]
    public async Task<IActionResult> RunQuotas([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        this.From(await requests.RunQuotasAsync(year, month, ct));
}

[ApiController]
[Route("api/supply/counts")]
[Authorize]
public sealed class SupplyCountsController(ISupplyCountService counts) : ControllerBase
{
    [HttpGet]
    [HasPermission("SupplyCount.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await counts.ListAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("SupplyCount.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await counts.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Create([FromBody] SaveCountCycleRequest r, CancellationToken ct) => this.From(await counts.CreateAsync(r, ct));

    [HttpPut("{id:long}")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCountCycleRequest r, CancellationToken ct) => this.From(await counts.UpdateAsync(id, r, ct));

    [HttpPost("{id:long}/start")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Start(long id, CancellationToken ct) => this.From(await counts.StartAsync(id, ct));

    [HttpGet("{id:long}/sheet")]
    [HasPermission("SupplyCount.Edit")]
    public async Task<IActionResult> Sheet(long id, CancellationToken ct) => this.From(await counts.SheetAsync(id, ct));

    [HttpGet("{id:long}/entries")]
    [HasPermission("SupplyCount.Edit")]
    public async Task<IActionResult> Entries(long id, CancellationToken ct) => this.From(await counts.EntriesAsync(id, ct));

    [HttpPost("{id:long}/entries")]
    [HasPermission("SupplyCount.Edit")]
    public async Task<IActionResult> Enter(long id, [FromBody] SaveCountEntry e, CancellationToken ct) => this.From(await counts.EnterAsync(id, e, ct));

    [HttpDelete("{id:long}/entries/{entryId:long}")]
    [HasPermission("SupplyCount.Edit")]
    public async Task<IActionResult> DeleteEntry(long id, long entryId, CancellationToken ct) => this.Deleted(await counts.DeleteEntryAsync(id, entryId, ct));

    [HttpPost("{id:long}/reconcile")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Reconcile(long id, CancellationToken ct) => this.From(await counts.ReconcileAsync(id, ct));

    [HttpGet("{id:long}/variances")]
    [HasPermission("SupplyCount.View")]
    public async Task<IActionResult> Variances(long id, CancellationToken ct) => this.From(await counts.VariancesAsync(id, ct));

    [HttpPut("{id:long}/variances/{varianceId:long}")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Justify(long id, long varianceId, [FromBody] JustifyBody b, CancellationToken ct) =>
        this.From(await counts.JustifyAsync(id, varianceId, b.Justification, ct));

    [HttpPost("{id:long}/settle")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Settle(long id, CancellationToken ct) => this.From(await counts.SettleAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("SupplyCount.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => this.From(await counts.CancelAsync(id, ct));
}

[ApiController]
[Route("api/supply/maintenance")]
[Authorize]
public sealed class SupplyMaintenanceController(IMaintenanceService maintenance) : ControllerBase
{
    [HttpGet]
    [HasPermission("SupplyMaintenance.Create")]
    public async Task<IActionResult> List([FromQuery] WorkOrderQuery q, CancellationToken ct) => Ok(await maintenance.ListAsync(q, ct));

    [HttpGet("counts")]
    [HasPermission("SupplyMaintenance.Create")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await maintenance.CountsAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission("SupplyMaintenance.Create")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await maintenance.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("SupplyMaintenance.Create")]
    public async Task<IActionResult> Report([FromBody] ReportFaultRequest r, CancellationToken ct) => this.From(await maintenance.ReportAsync(r, ct));

    [HttpPut("{id:long}")]
    [HasPermission("SupplyMaintenance.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateWorkOrderRequest r, CancellationToken ct) => this.From(await maintenance.UpdateAsync(id, r, ct));

    [HttpPost("{id:long}/parts")]
    [HasPermission("SupplyMaintenance.Edit")]
    public async Task<IActionResult> Parts(long id, [FromBody] WorkOrderPartsRequest r, CancellationToken ct) => this.From(await maintenance.IssuePartsAsync(id, r, ct));

    [HttpPost("{id:long}/close")]
    [HasPermission("SupplyMaintenance.Edit")]
    public async Task<IActionResult> Close(long id, [FromBody] CloseWorkOrderRequest r, CancellationToken ct) => this.From(await maintenance.CloseAsync(id, r, ct));
}

[ApiController]
[Route("api/supply/opening")]
[Authorize]
public sealed class SupplyOpeningController(IOpeningBalanceService opening) : ControllerBase
{
    [HttpGet]
    [HasPermission("SupplyOpening.View")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await opening.StatusAsync(ct));

    [HttpGet("template")]
    [HasPermission("SupplyOpening.View")]
    public IActionResult Template() => File(Encoding.UTF8.GetBytes(opening.Template()), "text/csv; charset=utf-8", "opening_balances.csv");

    [HttpPost("upload")]
    [HasPermission("SupplyOpening.Edit")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Upload([FromBody] UploadOpeningRequest r, CancellationToken ct) => this.From(await opening.UploadAsync(r, ct));

    [HttpPost("lock")]
    [HasPermission("SupplyOpening.Edit")]
    public async Task<IActionResult> Lock(CancellationToken ct) => this.From(await opening.LockAsync(ct));
}
