using Archiving.Application.Common.Models;

namespace Archiving.Application.Features.Supply;

// اللوازم والمخازن. Enum values travel as their names. Quantities decimal, money decimal(18,2).

// ---- Settings, catalogue, warehouses, quotas ----

public sealed record SupplySettingsDto(IReadOnlyDictionary<string, long?> Workflows, decimal? ReplacementCostPercent,
    int? WarrantyAlertDays, int? SuspendedRequestDays, int? StagnantDays, int? CountOverdueDays, DateOnly? OpeningLockedOn);
public sealed record SaveSupplySettingsRequest(IReadOnlyDictionary<string, long?> Workflows, decimal? ReplacementCostPercent,
    int? WarrantyAlertDays, int? SuspendedRequestDays, int? StagnantDays, int? CountOverdueDays);

public sealed record ItemCategoryDto(long Id, long? ParentId, string Code, string Name, string Kind, bool IsActive, int ItemCount, string RowVersion);
public sealed record SaveItemCategoryRequest(long? ParentId, string Code, string Name, string Kind, bool IsActive, string? RowVersion = null);

public sealed record ItemDto(long Id, string Code, string Name, long CategoryId, string CategoryName, string Kind, string Unit,
    decimal? ReorderLevel, decimal? MinLevel, int? UsefulLifeMonths, int? WarrantyMonths, bool IsActive, string RowVersion);
public sealed record SaveItemRequest(string Code, string Name, long CategoryId, string Unit, decimal? ReorderLevel, decimal? MinLevel,
    int? UsefulLifeMonths, int? WarrantyMonths, bool IsActive, string? RowVersion = null);

/// <summary>Catalogue row with stock per warehouse (دليل الأصناف). Total = sum over warehouses; Low = total ≤ reorder level.</summary>
public sealed record ItemStockDto(ItemDto Item, IReadOnlyDictionary<long, decimal> ByWarehouse, decimal Total, bool Low);

public sealed record WarehouseDto(long Id, string Code, string Name, string Kind, long? SiteId, string? SiteName,
    long? KeeperEmployeeId, string? KeeperName, bool IsActive, bool IsMine, string RowVersion);
public sealed record SaveWarehouseRequest(string Code, string Name, string Kind, long? SiteId, long? KeeperEmployeeId, bool IsActive,
    string? RowVersion = null);

public sealed record MonthlyQuotaDto(long Id, long OrgUnitId, string OrgUnitName, long ItemId, string ItemName, string Unit,
    long WarehouseId, string WarehouseName, decimal Quantity, bool IsActive);
public sealed record SaveMonthlyQuotaRequest(long OrgUnitId, long ItemId, long WarehouseId, decimal Quantity, bool IsActive);

public interface ISupplyCatalogService
{
    Task<SupplySettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<Result<SupplySettingsDto>> SaveSettingsAsync(SaveSupplySettingsRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ItemCategoryDto>> CategoriesAsync(CancellationToken ct = default);
    Task<Result<ItemCategoryDto>> SaveCategoryAsync(long? id, SaveItemCategoryRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ItemDto>> ItemsAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result<ItemDto>> SaveItemAsync(long? id, SaveItemRequest request, CancellationToken ct = default);

    /// <summary>All warehouses (names are not sensitive: requesters pick one). IsMine marks the caller's scope.</summary>
    Task<IReadOnlyList<WarehouseDto>> WarehousesAsync(CancellationToken ct = default);
    Task<Result<WarehouseDto>> SaveWarehouseAsync(long? id, SaveWarehouseRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MonthlyQuotaDto>> QuotasAsync(CancellationToken ct = default);
    Task<Result<MonthlyQuotaDto>> SaveQuotaAsync(long? id, SaveMonthlyQuotaRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteQuotaAsync(long id, CancellationToken ct = default);
}

// ---- Stock, assets, custody, dashboard, reports ----

public sealed record AssetQuery(string? Search, string? Status, long? ItemId, long? WarehouseId, long? EmployeeId, long? OrgUnitId,
    int Page = 1, int PageSize = 25);

public sealed record AssetDto(long Id, string AssetNumber, string Barcode, long ItemId, string ItemName, string? SerialNumber,
    string Status, string Condition, long? WarehouseId, string? WarehouseName, string? HolderType, long? HolderEmployeeId,
    long? HolderOrgUnitId, string? HolderName, DateOnly? PurchaseDate, decimal? Value, DateOnly? WarrantyEndDate,
    DateOnly? LastCountedDate, string RowVersion);

public sealed record AssetEventDto(DateTime At, string Kind, string? DocumentNumber, long? DocumentId, string Description);
public sealed record AssetWorkOrderDto(long Id, string Number, string Description, string Status, decimal Cost, DateTime CreatedAt);
public sealed record AssetCardDto(AssetDto Asset, IReadOnlyList<AssetEventDto> History, IReadOnlyList<AssetWorkOrderDto> Maintenance,
    int FaultCount, decimal MaintenanceCost, bool SuggestReplacement);

public sealed record CustodyQuery(long? EmployeeId, long? OrgUnitId, bool IncludeStaff = true);
/// <summary>كشف العهدة: open custody of an employee, or of a unit (its own + its staff's), plus the unit's consumption this month.</summary>
public sealed record CustodySheetDto(IReadOnlyList<AssetDto> Assets, int AssetCount, decimal TotalValue, decimal ConsumptionThisMonth);

public sealed record SupplyAlertDto(string Kind, string Title, string? Detail, string? Link);
public sealed record ConsumptionPointDto(int Year, int Month, long OrgUnitId, string OrgUnitName, decimal Quantity);
public sealed record LowStockDto(long ItemId, string ItemName, string Unit, decimal Balance, decimal? ReorderLevel, decimal? MinLevel);
public sealed record SupplyDashboardDto(int CriticalStock, int PendingRequests, int OpenWorkOrders, int AssetsInCustody,
    IReadOnlyList<LowStockDto> LowStock, IReadOnlyList<ConsumptionPointDto> Consumption,
    IReadOnlyList<IssueRequestDto> AwaitingFulfilment, IReadOnlyList<WorkOrderDto> WorkOrders, IReadOnlyList<SupplyAlertDto> Alerts);

public sealed record ItemCardLineDto(DateOnly Date, string? DocumentNumber, long DocumentId, string DocumentType, long WarehouseId,
    string WarehouseName, decimal In, decimal Out, decimal Balance);
public sealed record ItemCardDto(ItemDto Item, decimal Opening, IReadOnlyList<ItemCardLineDto> Lines, decimal Closing);
public sealed record WarehouseBalanceDto(long ItemId, string ItemCode, string ItemName, string Unit, string Kind, long WarehouseId,
    string WarehouseName, decimal Balance);
public sealed record ConsumptionRowDto(long OrgUnitId, string OrgUnitName, long ItemId, string ItemName, string Unit,
    decimal Quantity, decimal PreviousQuantity);
public sealed record MaintenanceCostRowDto(long? AssetId, string? AssetNumber, string? ItemName, int Orders, decimal Cost);
public sealed record StagnantItemDto(long ItemId, string ItemName, string Unit, decimal Balance, DateOnly? LastMovement);

public interface ISupplyStockService
{
    Task<IReadOnlyList<ItemStockDto>> CatalogueStockAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, decimal>> AvailableAsync(long warehouseId, CancellationToken ct = default);
    Task<PagedResult<AssetDto>> AssetsAsync(AssetQuery query, CancellationToken ct = default);
    Task<Result<AssetDto>> FindByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<Result<AssetCardDto>> AssetCardAsync(long id, CancellationToken ct = default);
    Task<Result<CustodySheetDto>> CustodyAsync(CustodyQuery query, CancellationToken ct = default);
    /// <summary>The signed-in employee's own custody (self-service / employee file).</summary>
    Task<IReadOnlyList<AssetDto>> MyCustodyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssetDto>> EmployeeCustodyAsync(long employeeId, CancellationToken ct = default);
    Task<SupplyDashboardDto> DashboardAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WarehouseBalanceDto>> BalancesReportAsync(long? warehouseId, CancellationToken ct = default);
    Task<Result<ItemCardDto>> ItemCardAsync(long itemId, long? warehouseId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<ConsumptionRowDto>> ConsumptionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceCostRowDto>> MaintenanceReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<StagnantItemDto>> StagnantReportAsync(CancellationToken ct = default);
}

// ---- Documents (المحاضر) ----

public sealed record DocumentQuery(string? Type, string? Status, string? Search, int Page = 1, int PageSize = 25);

public sealed record DocumentLineDto(long Id, long ItemId, string ItemName, string ItemKind, string Unit, decimal Quantity,
    long? AssetId, string? AssetNumber, string? SerialNumber, string? Barcode, string? Condition, decimal? UnitValue,
    DateOnly? PurchaseDate, long? WarehouseId, long? HolderEmployeeId, long? HolderOrgUnitId, string? Note);

public sealed record SupplyDocumentDto(long Id, string? Number, string Type, string Status, DateOnly DocDate, string Title, string? Notes,
    long? WarehouseId, string? WarehouseName, long? TargetWarehouseId, string? TargetWarehouseName,
    long? RecipientEmployeeId, long? RecipientOrgUnitId, string? RecipientName, string? Supplier, string? InvoiceNumber,
    string? ReceivedByName, long? IssueRequestId, long? CountCycleId, long? WorkOrderId, long? ApprovalRequestId,
    DateTime? ExecutedAt, string? DecisionNote, string? CreatedByName, DateTime CreatedAt, IReadOnlyList<DocumentLineDto> Lines,
    string RowVersion);

public sealed record SaveDocumentLine(long ItemId, decimal Quantity, long? AssetId, string? SerialNumber, string? Barcode,
    string? Condition, decimal? UnitValue, DateOnly? PurchaseDate, long? WarehouseId, long? HolderEmployeeId, long? HolderOrgUnitId,
    string? Note);

public sealed record SaveDocumentRequest(string Type, DateOnly DocDate, string Title, string? Notes, long? WarehouseId,
    long? TargetWarehouseId, long? RecipientEmployeeId, long? RecipientOrgUnitId, string? Supplier, string? InvoiceNumber,
    string? ReceivedByName, IReadOnlyList<SaveDocumentLine> Lines, string? RowVersion = null);

public sealed record DocumentCountsDto(IReadOnlyDictionary<string, int> ByType, int Total);

public interface ISupplyDocumentService
{
    Task<PagedResult<SupplyDocumentDto>> ListAsync(DocumentQuery query, CancellationToken ct = default);
    Task<DocumentCountsDto> CountsAsync(CancellationToken ct = default);
    Task<Result<SupplyDocumentDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<SupplyDocumentDto>> CreateAsync(SaveDocumentRequest request, CancellationToken ct = default);
    Task<Result<SupplyDocumentDto>> UpdateAsync(long id, SaveDocumentRequest request, CancellationToken ct = default);
    /// <summary>Numbers the document and sends it into its type's approval path.</summary>
    Task<Result<SupplyDocumentDto>> SubmitAsync(long id, CancellationToken ct = default);
    Task<Result<SupplyDocumentDto>> CancelAsync(long id, CancellationToken ct = default);
    /// <summary>MOV: the receiving keeper confirms; only now does stock leave one warehouse and enter the other.</summary>
    Task<Result<SupplyDocumentDto>> ConfirmReceiptAsync(long id, CancellationToken ct = default);
    /// <summary>Records that the document was printed (audited).</summary>
    Task<Result<bool>> PrintedAsync(long id, CancellationToken ct = default);
}

// ---- Issue requests (طلبات الصرف) ----

public sealed record IssueRequestQuery(string? Status, bool Mine, int Page = 1, int PageSize = 25);
public sealed record IssueRequestLineDto(long Id, long ItemId, string ItemName, string ItemKind, string Unit, decimal Quantity,
    decimal IssuedQuantity, decimal Available);
public sealed record IssueRequestDto(long Id, string? Number, long RequesterEmployeeId, string? RequesterName, long OrgUnitId,
    string? OrgUnitName, bool ForDepartment, long WarehouseId, string? WarehouseName, string Justification, string Status,
    long? ApprovalRequestId, DateTime? DecidedAt, DateTime? SuspendedAt, string? DecisionNote, DateTime CreatedAt,
    IReadOnlyList<IssueRequestLineDto> Lines, IReadOnlyList<long> DocumentIds, string RowVersion);
public sealed record SaveIssueRequestLine(long ItemId, decimal Quantity);
public sealed record SaveIssueRequest(long? EmployeeId, bool ForDepartment, long WarehouseId, string Justification,
    IReadOnlyList<SaveIssueRequestLine> Lines, string? RowVersion = null);
/// <summary>The keeper's fulfilment: quantities per line, and the asset pieces picked by barcode for asset items.</summary>
public sealed record FulfilLine(long LineId, decimal Quantity, IReadOnlyList<long>? AssetIds);
public sealed record FulfilIssueRequest(IReadOnlyList<FulfilLine> Lines, string? ReceivedByName, string? RowVersion);
public sealed record QuotaRunResult(int Created, int Skipped);

public interface IIssueRequestService
{
    Task<PagedResult<IssueRequestDto>> ListAsync(IssueRequestQuery query, CancellationToken ct = default);
    Task<Result<IssueRequestDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<IssueRequestDto>> CreateAsync(SaveIssueRequest request, CancellationToken ct = default);
    Task<Result<IssueRequestDto>> UpdateAsync(long id, SaveIssueRequest request, CancellationToken ct = default);
    Task<Result<IssueRequestDto>> SubmitAsync(long id, CancellationToken ct = default);
    Task<Result<IssueRequestDto>> CancelAsync(long id, CancellationToken ct = default);
    /// <summary>Keeper: issue what is available as an executed ISS document; the rest stays pending.</summary>
    Task<Result<IssueRequestDto>> FulfilAsync(long id, FulfilIssueRequest request, CancellationToken ct = default);
    /// <summary>Keeper: nothing available — suspend and raise a shortage alert.</summary>
    Task<Result<IssueRequestDto>> SuspendAsync(long id, string reason, CancellationToken ct = default);
    /// <summary>Creates and submits this month's request for every department with an active quota. Idempotent per month.</summary>
    Task<Result<QuotaRunResult>> RunQuotasAsync(int year, int month, CancellationToken ct = default);
}

// ---- Counts (الجرد) ----

public sealed record CountCycleDto(long Id, string? Number, string Name, string Scope, long? WarehouseId, string? WarehouseName,
    long? OrgUnitId, string? OrgUnitName, long? SiteId, string? SiteName, DateOnly PlannedDate, bool FreezeMovements, string Status,
    IReadOnlyList<long> CommitteeEmployeeIds, IReadOnlyList<string> CommitteeNames, int EntryCount, long? AdjustmentDocumentId,
    DateTime? ClosedAt, bool IsCommitteeMember, string RowVersion);
public sealed record SaveCountCycleRequest(string Name, string Scope, long? WarehouseId, long? OrgUnitId, long? SiteId,
    DateOnly PlannedDate, bool FreezeMovements, IReadOnlyList<long> CommitteeEmployeeIds, string? RowVersion = null);
/// <summary>The blind sheet: what to look for, never how many are expected.</summary>
public sealed record CountSheetLineDto(long ItemId, string ItemCode, string ItemName, string Unit, string Kind, long? WarehouseId,
    string? WarehouseName);
public sealed record CountEntryDto(long Id, long? ItemId, string? ItemName, long? WarehouseId, decimal? Quantity, string? Barcode,
    long? AssetId, string? AssetNumber, string? Condition, string? Note, DateTime CreatedAt);
public sealed record SaveCountEntry(long? ItemId, long? WarehouseId, decimal? Quantity, string? Barcode, string? Condition, string? Note);
public sealed record CountVarianceDto(long Id, string Kind, long? ItemId, string? ItemName, long? AssetId, string? AssetNumber,
    string? Barcode, long? WarehouseId, string? WarehouseName, decimal Expected, decimal Counted, decimal Difference,
    string? CountedCondition, string? Description, string? Justification);

public interface ISupplyCountService
{
    Task<IReadOnlyList<CountCycleDto>> ListAsync(CancellationToken ct = default);
    Task<Result<CountCycleDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<CountCycleDto>> CreateAsync(SaveCountCycleRequest request, CancellationToken ct = default);
    Task<Result<CountCycleDto>> UpdateAsync(long id, SaveCountCycleRequest request, CancellationToken ct = default);
    Task<Result<CountCycleDto>> StartAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CountSheetLineDto>>> SheetAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CountEntryDto>>> EntriesAsync(long id, CancellationToken ct = default);
    /// <summary>Records a count. The response never carries the expected quantity (blind count, standards §8.3).</summary>
    Task<Result<CountEntryDto>> EnterAsync(long id, SaveCountEntry entry, CancellationToken ct = default);
    Task<Result<bool>> DeleteEntryAsync(long id, long entryId, CancellationToken ct = default);
    /// <summary>Ends counting and compares with the records, on the server.</summary>
    Task<Result<IReadOnlyList<CountVarianceDto>>> ReconcileAsync(long id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CountVarianceDto>>> VariancesAsync(long id, CancellationToken ct = default);
    Task<Result<CountVarianceDto>> JustifyAsync(long id, long varianceId, string justification, CancellationToken ct = default);
    /// <summary>All variances justified → ADJ document into approval (or close outright when there are none).</summary>
    Task<Result<CountCycleDto>> SettleAsync(long id, CancellationToken ct = default);
    Task<Result<CountCycleDto>> CancelAsync(long id, CancellationToken ct = default);
}

// ---- Maintenance (الصيانة) ----

public sealed record WorkOrderQuery(string? Folder, string? Search, int Page = 1, int PageSize = 25);
public sealed record WorkOrderDto(long Id, string Number, long? AssetId, string? AssetNumber, string? ItemName, string? Location,
    long? SiteId, string Description, long ReporterEmployeeId, string? ReporterName, long? AssigneeEmployeeId, string? AssigneeName,
    string Status, string? Assessment, decimal? LaborHours, decimal LaborCost, decimal PartsCost, DateTime CreatedAt,
    DateTime? ClosedAt, string? Outcome, bool SuggestReplacement, IReadOnlyList<long> DocumentIds, string RowVersion);
public sealed record ReportFaultRequest(string? Barcode, string? Location, long? SiteId, string Description);
public sealed record UpdateWorkOrderRequest(long? AssigneeEmployeeId, string Status, string? Assessment, string? RowVersion);
public sealed record WorkOrderPartsRequest(long WarehouseId, IReadOnlyList<SaveIssueRequestLine> Lines, string? RowVersion);
public sealed record CloseWorkOrderRequest(bool Repaired, decimal? LaborHours, decimal LaborCost, string Outcome, string? RowVersion);
public sealed record WorkOrderCountsDto(int Open, int Mine, int Closed);

public interface IMaintenanceService
{
    Task<PagedResult<WorkOrderDto>> ListAsync(WorkOrderQuery query, CancellationToken ct = default);
    Task<WorkOrderCountsDto> CountsAsync(CancellationToken ct = default);
    Task<Result<WorkOrderDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<WorkOrderDto>> ReportAsync(ReportFaultRequest request, CancellationToken ct = default);
    Task<Result<WorkOrderDto>> UpdateAsync(long id, UpdateWorkOrderRequest request, CancellationToken ct = default);
    /// <summary>Issues maintenance supplies on the work order (executed ISS document from a maintenance warehouse).</summary>
    Task<Result<WorkOrderDto>> IssuePartsAsync(long id, WorkOrderPartsRequest request, CancellationToken ct = default);
    Task<Result<WorkOrderDto>> CloseAsync(long id, CloseWorkOrderRequest request, CancellationToken ct = default);
}

// ---- Opening balances (الأرصدة الافتتاحية) ----

public sealed record OpeningErrorDto(int Line, string Message);
public sealed record OpeningImportDto(long Id, string FileName, string Stage, int RowCount, string Status,
    IReadOnlyList<OpeningErrorDto> Errors, long? DocumentId, string? DocumentNumber, string? DocumentStatus, DateTime CreatedAt);
public sealed record UploadOpeningRequest(string FileName, string Stage, string Content);
public sealed record OpeningStatusDto(bool Locked, DateOnly? LockedOn, IReadOnlyList<OpeningImportDto> Imports);

public interface IOpeningBalanceService
{
    string Template();
    Task<OpeningStatusDto> StatusAsync(CancellationToken ct = default);
    /// <summary>Checks the file; when clean, creates the draft SUP-OPN document. Errors come back as a report.</summary>
    Task<Result<OpeningImportDto>> UploadAsync(UploadOpeningRequest request, CancellationToken ct = default);
    Task<Result<OpeningStatusDto>> LockAsync(CancellationToken ct = default);
}
