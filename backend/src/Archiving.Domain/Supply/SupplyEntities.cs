using Archiving.Domain.Core;

namespace Archiving.Domain.Supply;

// اللوازم والمخازن (process sequence §5–7, presentation slides 26–36). Schema "supply".
// Golden rule: no balance is ever edited. Warehouse stock = sum of stock_movements, and every movement
// points at the numbered document (محضر) that caused it. Assets are never deleted: a disposed asset keeps
// its full history with status Disposed.

/// <summary>Keyed supply setting. Keys are listed in <see cref="SupplySettingKeys"/>.</summary>
public class SupplySetting : CoreEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class SupplySettingKeys
{
    /// <summary>Approval path for a document type: "workflow.RCV", "workflow.ISS", … and "workflow.REQ" for issue requests.</summary>
    public static string Workflow(string type) => $"workflow.{type}";
    public const string IssueRequestWorkflow = "workflow.REQ";
    /// <summary>Accumulated maintenance cost as % of asset value above which replacement is suggested. Empty = never.</summary>
    public const string ReplacementCostPercent = "replacement_cost_percent";
    /// <summary>Days before warranty end to raise an alert.</summary>
    public const string WarrantyAlertDays = "warranty_alert_days";
    /// <summary>Days an issue request may stay suspended for shortage before it is flagged.</summary>
    public const string SuspendedRequestDays = "suspended_request_days";
    /// <summary>Days without any movement after which an item counts as stagnant (راكد).</summary>
    public const string StagnantDays = "stagnant_days";
    /// <summary>Days since last count after which an asset is flagged (the sequence says one year).</summary>
    public const string CountOverdueDays = "count_overdue_days";
    /// <summary>Set once by the opening wizard's last step. After it no opening document is accepted.</summary>
    public const string OpeningLockedOn = "opening_locked_on";

    public static readonly IReadOnlySet<string> Editable = new HashSet<string>(StringComparer.Ordinal)
    {
        ReplacementCostPercent, WarrantyAlertDays, SuspendedRequestDays, StagnantDays, CountOverdueDays,
    };
}

/// <summary>How an item is tracked (§5.1).</summary>
public enum ItemKind
{
    FixedAsset = 0,         // أصل ثابت: per piece, asset number, serial, barcode → custody
    Consumable = 1,         // مستهلك: quantity only → consumption on the department
    MaintenanceSupply = 2,  // مستلزم صيانة: quantity only → issued on a work order
}

/// <summary>Category tree: main category → sub-category → item (§5.1).</summary>
public class ItemCategory : CoreEntity
{
    public long? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Item : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public ItemKind Kind { get; set; }
    public string Unit { get; set; } = string.Empty;          // وحدة القياس
    /// <summary>حد الطلب: at or below it the item is "critical stock". Null = not monitored.</summary>
    public decimal? ReorderLevel { get; set; }
    public decimal? MinLevel { get; set; }                     // الحد الأدنى
    public int? UsefulLifeMonths { get; set; }                 // assets: العمر الإنتاجي
    public int? WarrantyMonths { get; set; }                   // assets: مدة الضمان
    public bool IsActive { get; set; } = true;

    public ItemCategory Category { get; set; } = null!;
}

public enum WarehouseKind { Main = 0, Site = 1, Maintenance = 2 }

/// <summary>A main warehouse, one per crossing, and a maintenance store (§5.2). Each has one keeper.</summary>
public class Warehouse : CoreEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WarehouseKind Kind { get; set; }
    public long? SiteId { get; set; }                 // core.sites
    public long? KeeperEmployeeId { get; set; }       // أمين المستودع: their scope is this warehouse only
    public bool IsActive { get; set; } = true;
}

public enum AssetStatus
{
    InStock = 0,          // في المستودع
    InCustody = 1,        // في عهدة
    Maintenance = 2,      // صيانة
    PendingDisposal = 3,  // محال للإتلاف
    Disposed = 4,         // مشطوب — kept forever
    Missing = 5,          // مفقود بعد جرد معتمد، بانتظار قرار الشطب
}

public enum AssetCondition { Good = 0, NeedsMaintenance = 1, Damaged = 2 }

/// <summary>One physical piece of a fixed-asset item.</summary>
public class Asset : CoreEntity
{
    public string AssetNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public long ItemId { get; set; }
    public string? SerialNumber { get; set; }
    public AssetStatus Status { get; set; }
    public AssetCondition Condition { get; set; }
    /// <summary>The warehouse holding it while InStock / Maintenance from stock; null while in custody.</summary>
    public long? WarehouseId { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? Value { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public DateOnly? LastCountedDate { get; set; }
    /// <summary>The status to return to when an open work order closes.</summary>
    public AssetStatus? StatusBeforeMaintenance { get; set; }

    public Item Item { get; set; } = null!;
}

public enum CustodyHolder { Employee = 0, OrgUnit = 1 }

/// <summary>
/// One period of an asset in someone's hands (§6.4). Personal (an employee) or departmental (a unit whose
/// manager is answerable). Closed, never edited, when the asset moves on.
/// </summary>
public class Custody : CoreEntity
{
    public long AssetId { get; set; }
    public CustodyHolder HolderType { get; set; }
    public long? EmployeeId { get; set; }
    public long? OrgUnitId { get; set; }
    /// <summary>Personal custody: the employee's unit when it was handed over, to flag custody left behind on transfer.</summary>
    public long? HolderOrgUnitId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long OpenedByDocumentId { get; set; }
    public long? ClosedByDocumentId { get; set; }
}

/// <summary>The eight document types (§5.5, §6.1).</summary>
public enum DocumentType
{
    RCV = 0,  // استلام من مورد
    ISS = 1,  // صرف
    TRF = 2,  // نقل عهدة
    MOV = 3,  // نقل بين مستودعات
    RET = 4,  // إرجاع
    DSP = 5,  // إتلاف وشطب
    ADJ = 6,  // تسوية جرد
    OPN = 7,  // رصيد افتتاحي
}

public enum DocumentStatus
{
    Draft = 0,
    Submitted = 1,        // in the approval engine
    AwaitingReceipt = 2,  // MOV approved; stock moves only when the receiving keeper confirms
    Executed = 3,         // منفذ
    Rejected = 4,
    Returned = 5,
    Cancelled = 6,
}

/// <summary>محضر: the only thing that changes stock or custody.</summary>
public class SupplyDocument : CoreEntity
{
    public string? Number { get; set; }               // SUP-<TYPE>-YYYY-NNNNN, issued on submission
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateOnly DocDate { get; set; }
    public string Title { get; set; } = string.Empty;  // البيان
    public string? Notes { get; set; }
    public long? WarehouseId { get; set; }             // source (ISS, MOV, DSP from stock) or receiving (RCV, RET)
    public long? TargetWarehouseId { get; set; }       // MOV
    public long? RecipientEmployeeId { get; set; }     // ISS/TRF personal custody
    public long? RecipientOrgUnitId { get; set; }      // ISS consumption / departmental custody, TRF to a unit
    public string? Supplier { get; set; }              // RCV
    public string? InvoiceNumber { get; set; }         // RCV
    public string? ReceivedByName { get; set; }        // توقيع المستلم (name as signed)
    public long? IssueRequestId { get; set; }
    public long? CountCycleId { get; set; }
    public long? WorkOrderId { get; set; }
    public long? OpeningImportId { get; set; }
    public long? ApprovalRequestId { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? DecisionNote { get; set; }

    public ICollection<SupplyDocumentLine> Lines { get; set; } = new List<SupplyDocumentLine>();
}

public class SupplyDocumentLine : CoreEntity
{
    public long DocumentId { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }              // positive; ADJ may be negative (shortage)
    public long? AssetId { get; set; }                 // existing piece (ISS, TRF, MOV, RET, DSP, ADJ)
    public string? SerialNumber { get; set; }          // new piece (RCV, OPN)
    public string? Barcode { get; set; }               // OPN: the label already stuck on the piece
    public AssetCondition? Condition { get; set; }     // RET inspection, OPN, ADJ condition change
    public decimal? UnitValue { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public long? WarehouseId { get; set; }             // OPN / ADJ: per-line warehouse
    public long? HolderEmployeeId { get; set; }        // OPN: custody owner
    public long? HolderOrgUnitId { get; set; }
    public string? Note { get; set; }
}

/// <summary>The only way warehouse stock changes. Balance = sum(Quantity) per item and warehouse.</summary>
public class StockMovement : CoreEntity
{
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public decimal Quantity { get; set; }              // signed
    public long DocumentId { get; set; }
    public long? AssetId { get; set; }
    /// <summary>ISS of a consumable: the consuming department, for consumption reports.</summary>
    public long? OrgUnitId { get; set; }
    public long? WorkOrderId { get; set; }
    public DateOnly MovementDate { get; set; }
}

public enum IssueRequestStatus
{
    Draft = 0,
    Submitted = 1,         // in approval
    Approved = 2,          // waiting for the keeper
    PartiallyIssued = 3,   // the rest is pending
    Issued = 4,
    Suspended = 5,         // nothing available: shortage alert raised
    Rejected = 6,
    Returned = 7,
    Cancelled = 8,
}

/// <summary>طلب صرف (§6.3): employee or department → direct manager → supply manager → keeper issues.</summary>
public class IssueRequest : CoreEntity
{
    public string? Number { get; set; }
    public long RequesterEmployeeId { get; set; }
    public long OrgUnitId { get; set; }                // the requesting department
    /// <summary>True: for the department (departmental custody / consumption). False: personal.</summary>
    public bool ForDepartment { get; set; }
    public long WarehouseId { get; set; }
    public string Justification { get; set; } = string.Empty;
    public IssueRequestStatus Status { get; set; } = IssueRequestStatus.Draft;
    public long? MonthlyQuotaRunId { get; set; }       // generated from a monthly quota (yyyymm)
    public long? ApprovalRequestId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? DecisionNote { get; set; }

    public ICollection<IssueRequestLine> Lines { get; set; } = new List<IssueRequestLine>();
}

public class IssueRequestLine : CoreEntity
{
    public long IssueRequestId { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal IssuedQuantity { get; set; }        // from executed ISS documents
}

/// <summary>Monthly consumable quota per department (§6.3): one auto request a month, approval only.</summary>
public class MonthlyQuota : CoreEntity
{
    public long OrgUnitId { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum CountScope { Warehouse = 0, OrgUnit = 1, Site = 2, All = 3 }

public enum CountStatus
{
    Planning = 0,   // تخطيط + اللجنة
    Counting = 1,   // العدّ (freeze applies here)
    Reconciled = 2, // المطابقة: variances computed, justifications entered
    Settling = 3,   // ADJ document in approval
    Closed = 4,
    Cancelled = 5,
}

/// <summary>دورة جرد (§7.1). The committee never sees expected quantities: comparison is server-side.</summary>
public class CountCycle : CoreEntity
{
    public string? Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public CountScope Scope { get; set; }
    public long? WarehouseId { get; set; }
    public long? OrgUnitId { get; set; }
    public long? SiteId { get; set; }
    public DateOnly PlannedDate { get; set; }
    public bool FreezeMovements { get; set; }
    public CountStatus Status { get; set; } = CountStatus.Planning;
    public long? AdjustmentDocumentId { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<CountCommitteeMember> Committee { get; set; } = new List<CountCommitteeMember>();
}

public class CountCommitteeMember : CoreEntity
{
    public long CountCycleId { get; set; }
    public long EmployeeId { get; set; }
}

/// <summary>One counted fact: a scanned asset barcode, or a quantity of an item in a warehouse.</summary>
public class CountEntry : CoreEntity
{
    public long CountCycleId { get; set; }
    public long? ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public decimal? Quantity { get; set; }
    public string? Barcode { get; set; }
    public long? AssetId { get; set; }
    public AssetCondition? Condition { get; set; }
    public string? Note { get; set; }
}

public enum VarianceKind
{
    Shortage = 0,          // نقص
    Surplus = 1,           // زيادة
    Misplaced = 2,         // أصل في غير مكانه
    Missing = 3,           // مفقود
    ConditionChanged = 4,  // حالة متغيرة
}

public class CountVariance : CoreEntity
{
    public long CountCycleId { get; set; }
    public VarianceKind Kind { get; set; }
    public long? ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public long? AssetId { get; set; }
    public string? Barcode { get; set; }
    public decimal Expected { get; set; }
    public decimal Counted { get; set; }
    public decimal Difference { get; set; }             // counted − expected
    public AssetCondition? CountedCondition { get; set; }
    public string? Description { get; set; }
    public string? Justification { get; set; }
}

public enum WorkOrderStatus
{
    New = 0,                  // بلاغ جديد
    InProgress = 1,           // قيد التنفيذ
    AwaitingParts = 2,        // بانتظار المواد
    Closed = 3,               // أُصلح وأُغلق
    RecommendedDisposal = 4,  // لم يُصلح: توصية بالإتلاف
    Cancelled = 5,
}

/// <summary>Fault report and its work order (§7.2). On an asset (barcode) or on a place.</summary>
public class WorkOrder : CoreEntity
{
    public string Number { get; set; } = string.Empty;  // WO-YYYY-NNNNN
    public long? AssetId { get; set; }
    public string? Location { get; set; }
    public long? SiteId { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? ReporterEmployeeId { get; set; }       // null when raised by the system (a returned piece needing repair)
    public long? AssigneeEmployeeId { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.New;
    public string? Assessment { get; set; }
    public decimal? LaborHours { get; set; }
    public decimal LaborCost { get; set; }
    public decimal PartsCost { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Outcome { get; set; }
}

public enum OpeningImportStatus { Failed = 0, Valid = 1, DocumentCreated = 2 }

/// <summary>One upload of the opening-balance template (§5.3), with its error report.</summary>
public class OpeningImport : CoreEntity
{
    public string FileName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;   // e.g. the department counted in this phase
    public int RowCount { get; set; }
    public OpeningImportStatus Status { get; set; }
    public string ErrorsJson { get; set; } = "[]";
    public long? DocumentId { get; set; }
}
