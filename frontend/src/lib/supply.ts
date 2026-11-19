import { api } from './api'
import type { Paged } from './hr'

/* Supply & warehouses client (اللوازم والمخازن). Enum values travel as names. */

const d = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data)

export const ITEM_KINDS = ['FixedAsset', 'Consumable', 'MaintenanceSupply'] as const
export type ItemKind = (typeof ITEM_KINDS)[number]
export const WAREHOUSE_KINDS = ['Main', 'Site', 'Maintenance'] as const
export const DOC_TYPES = ['RCV', 'ISS', 'TRF', 'MOV', 'RET', 'DSP', 'ADJ', 'OPN'] as const
export type DocType = (typeof DOC_TYPES)[number]
export const MANUAL_DOC_TYPES: DocType[] = ['RCV', 'ISS', 'TRF', 'MOV', 'RET', 'DSP']
export type DocStatus = 'Draft' | 'Submitted' | 'AwaitingReceipt' | 'Executed' | 'Rejected' | 'Returned' | 'Cancelled'
export type AssetStatus = 'InStock' | 'InCustody' | 'Maintenance' | 'PendingDisposal' | 'Disposed' | 'Missing'
export const ASSET_STATUSES: AssetStatus[] = ['InStock', 'InCustody', 'Maintenance', 'PendingDisposal', 'Disposed', 'Missing']
export const CONDITIONS = ['Good', 'NeedsMaintenance', 'Damaged'] as const
export type Condition = (typeof CONDITIONS)[number]
export type RequestStatus = 'Draft' | 'Submitted' | 'Approved' | 'PartiallyIssued' | 'Issued' | 'Suspended' | 'Rejected' | 'Returned' | 'Cancelled'
export type CountStatus = 'Planning' | 'Counting' | 'Reconciled' | 'Settling' | 'Closed' | 'Cancelled'
export const COUNT_SCOPES = ['Warehouse', 'OrgUnit', 'Site', 'All'] as const
export type WorkOrderStatus = 'New' | 'InProgress' | 'AwaitingParts' | 'Closed' | 'RecommendedDisposal' | 'Cancelled'

// ---- catalogue ----
export interface SupplySettingsDto {
  workflows: Record<string, number | null>; replacementCostPercent: number | null; warrantyAlertDays: number | null
  suspendedRequestDays: number | null; stagnantDays: number | null; countOverdueDays: number | null; openingLockedOn: string | null
}
export type SaveSupplySettings = Omit<SupplySettingsDto, 'openingLockedOn'>
export interface CategoryDto { id: number; parentId: number | null; code: string; name: string; kind: ItemKind; isActive: boolean; itemCount: number; rowVersion: string }
export interface SaveCategory { parentId: number | null; code: string; name: string; kind: ItemKind; isActive: boolean; rowVersion?: string | null }
export interface ItemDto {
  id: number; code: string; name: string; categoryId: number; categoryName: string; kind: ItemKind; unit: string
  reorderLevel: number | null; minLevel: number | null; usefulLifeMonths: number | null; warrantyMonths: number | null; isActive: boolean; rowVersion: string
}
export interface SaveItem {
  code: string; name: string; categoryId: number; unit: string; reorderLevel: number | null; minLevel: number | null
  usefulLifeMonths: number | null; warrantyMonths: number | null; isActive: boolean; rowVersion?: string | null
}
export interface ItemStockDto { item: ItemDto; byWarehouse: Record<string, number>; total: number; low: boolean }
export interface WarehouseDto {
  id: number; code: string; name: string; kind: (typeof WAREHOUSE_KINDS)[number]; siteId: number | null; siteName: string | null
  keeperEmployeeId: number | null; keeperName: string | null; isActive: boolean; isMine: boolean; rowVersion: string
}
export interface SaveWarehouse { code: string; name: string; kind: string; siteId: number | null; keeperEmployeeId: number | null; isActive: boolean; rowVersion?: string | null }
export interface QuotaDto { id: number; orgUnitId: number; orgUnitName: string; itemId: number; itemName: string; unit: string; warehouseId: number; warehouseName: string; quantity: number; isActive: boolean }
export interface SaveQuota { orgUnitId: number; itemId: number; warehouseId: number; quantity: number; isActive: boolean }

// ---- stock ----
export interface AssetDto {
  id: number; assetNumber: string; barcode: string; itemId: number; itemName: string; serialNumber: string | null; status: AssetStatus
  condition: Condition; warehouseId: number | null; warehouseName: string | null; holderType: 'Employee' | 'OrgUnit' | null
  holderEmployeeId: number | null; holderOrgUnitId: number | null; holderName: string | null; purchaseDate: string | null; value: number | null
  warrantyEndDate: string | null; lastCountedDate: string | null; rowVersion: string
}
export interface AssetCardDto {
  asset: AssetDto; history: { at: string; kind: string; documentNumber: string | null; documentId: number | null; description: string }[]
  maintenance: { id: number; number: string; description: string; status: WorkOrderStatus; cost: number; createdAt: string }[]
  faultCount: number; maintenanceCost: number; suggestReplacement: boolean
}
export interface CustodySheetDto { assets: AssetDto[]; assetCount: number; totalValue: number; consumptionThisMonth: number }
export interface DashboardDto {
  criticalStock: number; pendingRequests: number; openWorkOrders: number; assetsInCustody: number
  lowStock: { itemId: number; itemName: string; unit: string; balance: number; reorderLevel: number | null; minLevel: number | null }[]
  consumption: { year: number; month: number; orgUnitId: number; orgUnitName: string; quantity: number }[]
  awaitingFulfilment: IssueRequestDto[]; workOrders: WorkOrderDto[]
  alerts: { kind: string; title: string; detail: string | null; link: string | null }[]
}

// ---- documents ----
export interface DocLineDto {
  id: number; itemId: number; itemName: string; itemKind: ItemKind; unit: string; quantity: number; assetId: number | null; assetNumber: string | null
  serialNumber: string | null; barcode: string | null; condition: Condition | null; unitValue: number | null; purchaseDate: string | null
  warehouseId: number | null; holderEmployeeId: number | null; holderOrgUnitId: number | null; note: string | null
}
export interface DocumentDto {
  id: number; number: string | null; type: DocType; status: DocStatus; docDate: string; title: string; notes: string | null
  warehouseId: number | null; warehouseName: string | null; targetWarehouseId: number | null; targetWarehouseName: string | null
  recipientEmployeeId: number | null; recipientOrgUnitId: number | null; recipientName: string | null; supplier: string | null
  invoiceNumber: string | null; receivedByName: string | null; issueRequestId: number | null; countCycleId: number | null
  workOrderId: number | null; approvalRequestId: number | null; executedAt: string | null; decisionNote: string | null
  createdByName: string | null; createdAt: string; lines: DocLineDto[]; rowVersion: string
}
export interface SaveDocLine {
  itemId: number; quantity: number; assetId: number | null; serialNumber: string | null; barcode: string | null; condition: string | null
  unitValue: number | null; purchaseDate: string | null; warehouseId: number | null; holderEmployeeId: number | null; holderOrgUnitId: number | null; note: string | null
}
export interface SaveDocument {
  type: DocType; docDate: string; title: string; notes: string | null; warehouseId: number | null; targetWarehouseId: number | null
  recipientEmployeeId: number | null; recipientOrgUnitId: number | null; supplier: string | null; invoiceNumber: string | null
  receivedByName: string | null; lines: SaveDocLine[]; rowVersion?: string | null
}

// ---- requests ----
export interface IssueRequestDto {
  id: number; number: string | null; requesterEmployeeId: number; requesterName: string | null; orgUnitId: number; orgUnitName: string | null
  forDepartment: boolean; warehouseId: number; warehouseName: string | null; justification: string; status: RequestStatus
  approvalRequestId: number | null; decidedAt: string | null; suspendedAt: string | null; decisionNote: string | null; createdAt: string
  lines: { id: number; itemId: number; itemName: string; itemKind: ItemKind; unit: string; quantity: number; issuedQuantity: number; available: number }[]
  documentIds: number[]; rowVersion: string
}
export interface SaveIssueRequest {
  employeeId: number | null; forDepartment: boolean; warehouseId: number; justification: string
  lines: { itemId: number; quantity: number }[]; rowVersion?: string | null
}

// ---- counts ----
export interface CountCycleDto {
  id: number; number: string | null; name: string; scope: (typeof COUNT_SCOPES)[number]; warehouseId: number | null; warehouseName: string | null
  orgUnitId: number | null; orgUnitName: string | null; siteId: number | null; siteName: string | null; plannedDate: string; freezeMovements: boolean
  status: CountStatus; committeeEmployeeIds: number[]; committeeNames: string[]; entryCount: number; adjustmentDocumentId: number | null
  closedAt: string | null; isCommitteeMember: boolean; rowVersion: string
}
export interface SaveCountCycle {
  name: string; scope: string; warehouseId: number | null; orgUnitId: number | null; siteId: number | null; plannedDate: string
  freezeMovements: boolean; committeeEmployeeIds: number[]; rowVersion?: string | null
}
export interface SheetLineDto { itemId: number; itemCode: string; itemName: string; unit: string; kind: ItemKind; warehouseId: number | null; warehouseName: string | null }
export interface CountEntryDto {
  id: number; itemId: number | null; itemName: string | null; warehouseId: number | null; quantity: number | null; barcode: string | null
  assetId: number | null; assetNumber: string | null; condition: Condition | null; note: string | null; createdAt: string
}
export interface VarianceDto {
  id: number; kind: 'Shortage' | 'Surplus' | 'Misplaced' | 'Missing' | 'ConditionChanged'; itemId: number | null; itemName: string | null
  assetId: number | null; assetNumber: string | null; barcode: string | null; warehouseId: number | null; warehouseName: string | null
  expected: number; counted: number; difference: number; countedCondition: Condition | null; description: string | null; justification: string | null
}

// ---- maintenance ----
export interface WorkOrderDto {
  id: number; number: string; assetId: number | null; assetNumber: string | null; itemName: string | null; location: string | null
  siteId: number | null; description: string; reporterEmployeeId: number; reporterName: string | null; assigneeEmployeeId: number | null
  assigneeName: string | null; status: WorkOrderStatus; assessment: string | null; laborHours: number | null; laborCost: number
  partsCost: number; createdAt: string; closedAt: string | null; outcome: string | null; suggestReplacement: boolean
  documentIds: number[]; rowVersion: string
}

// ---- opening ----
export interface OpeningImportDto {
  id: number; fileName: string; stage: string; rowCount: number; status: 'Failed' | 'Valid' | 'DocumentCreated'
  errors: { line: number; message: string }[]; documentId: number | null; documentNumber: string | null; documentStatus: DocStatus | null; createdAt: string
}
export interface OpeningStatusDto { locked: boolean; lockedOn: string | null; imports: OpeningImportDto[] }

// ---- reports ----
export interface BalanceRow { itemId: number; itemCode: string; itemName: string; unit: string; kind: ItemKind; warehouseId: number; warehouseName: string; balance: number }
export interface ItemCardDto {
  item: ItemDto; opening: number; closing: number
  lines: { date: string; documentNumber: string | null; documentId: number; documentType: DocType; warehouseId: number; warehouseName: string; in: number; out: number; balance: number }[]
}
export interface ConsumptionRow { orgUnitId: number; orgUnitName: string; itemId: number; itemName: string; unit: string; quantity: number; previousQuantity: number }
export interface MaintenanceCostRow { assetId: number | null; assetNumber: string | null; itemName: string | null; orders: number; cost: number }
export interface StagnantRow { itemId: number; itemName: string; unit: string; balance: number; lastMovement: string | null }

const base = '/supply'

export const supply = {
  // catalogue
  settings: () => d(api.get<SupplySettingsDto>(`${base}/catalog/settings`)),
  saveSettings: (b: SaveSupplySettings) => d(api.put<SupplySettingsDto>(`${base}/catalog/settings`, b)),
  categories: () => d(api.get<CategoryDto[]>(`${base}/catalog/categories`)),
  saveCategory: (id: number | null, b: SaveCategory) =>
    d(id ? api.put<CategoryDto>(`${base}/catalog/categories/${id}`, b) : api.post<CategoryDto>(`${base}/catalog/categories`, b)),
  items: (activeOnly = false) => d(api.get<ItemDto[]>(`${base}/catalog/items`, { params: { activeOnly } })),
  saveItem: (id: number | null, b: SaveItem) =>
    d(id ? api.put<ItemDto>(`${base}/catalog/items/${id}`, b) : api.post<ItemDto>(`${base}/catalog/items`, b)),
  warehouses: () => d(api.get<WarehouseDto[]>(`${base}/catalog/warehouses`)),
  saveWarehouse: (id: number | null, b: SaveWarehouse) =>
    d(id ? api.put<WarehouseDto>(`${base}/catalog/warehouses/${id}`, b) : api.post<WarehouseDto>(`${base}/catalog/warehouses`, b)),
  quotas: () => d(api.get<QuotaDto[]>(`${base}/catalog/quotas`)),
  saveQuota: (id: number | null, b: SaveQuota) =>
    d(id ? api.put<QuotaDto>(`${base}/catalog/quotas/${id}`, b) : api.post<QuotaDto>(`${base}/catalog/quotas`, b)),
  deleteQuota: (id: number) => api.delete(`${base}/catalog/quotas/${id}`),

  // stock
  dashboard: () => d(api.get<DashboardDto>(`${base}/stock/dashboard`)),
  catalogue: () => d(api.get<ItemStockDto[]>(`${base}/stock/catalogue`)),
  available: (warehouseId: number) => d(api.get<Record<string, number>>(`${base}/stock/available/${warehouseId}`)),
  assets: (p: { search?: string; status?: string; itemId?: number; warehouseId?: number; page: number; pageSize: number }) =>
    d(api.get<Paged<AssetDto>>(`${base}/stock/assets`, { params: p })),
  byBarcode: (code: string) => d(api.get<AssetDto>(`${base}/stock/assets/by-barcode/${encodeURIComponent(code)}`)),
  assetCard: (id: number) => d(api.get<AssetCardDto>(`${base}/stock/assets/${id}`)),
  custody: (p: { employeeId?: number; orgUnitId?: number; includeStaff?: boolean }) => d(api.get<CustodySheetDto>(`${base}/stock/custody`, { params: p })),
  myCustody: () => d(api.get<AssetDto[]>(`${base}/stock/custody/mine`)),
  employeeCustody: (id: number) => d(api.get<AssetDto[]>(`${base}/stock/custody/employee/${id}`)),
  balances: (warehouseId?: number) => d(api.get<BalanceRow[]>(`${base}/stock/reports/balances`, { params: { warehouseId } })),
  itemCard: (itemId: number, from: string, to: string, warehouseId?: number) =>
    d(api.get<ItemCardDto>(`${base}/stock/reports/item-card/${itemId}`, { params: { from, to, warehouseId } })),
  consumption: (from: string, to: string) => d(api.get<ConsumptionRow[]>(`${base}/stock/reports/consumption`, { params: { from, to } })),
  maintenanceReport: (from: string, to: string) => d(api.get<MaintenanceCostRow[]>(`${base}/stock/reports/maintenance`, { params: { from, to } })),
  stagnant: () => d(api.get<StagnantRow[]>(`${base}/stock/reports/stagnant`)),

  // documents
  documents: (p: { type?: string; status?: string; search?: string; page: number; pageSize: number }) =>
    d(api.get<Paged<DocumentDto>>(`${base}/documents`, { params: p })),
  documentCounts: () => d(api.get<{ byType: Record<string, number>; total: number }>(`${base}/documents/counts`)),
  document: (id: number) => d(api.get<DocumentDto>(`${base}/documents/${id}`)),
  saveDocument: (id: number | null, b: SaveDocument) =>
    d(id ? api.put<DocumentDto>(`${base}/documents/${id}`, b) : api.post<DocumentDto>(`${base}/documents`, b)),
  submitDocument: (id: number) => d(api.post<DocumentDto>(`${base}/documents/${id}/submit`)),
  cancelDocument: (id: number) => d(api.post<DocumentDto>(`${base}/documents/${id}/cancel`)),
  confirmReceipt: (id: number) => d(api.post<DocumentDto>(`${base}/documents/${id}/confirm-receipt`)),
  documentPrinted: (id: number) => api.post(`${base}/documents/${id}/printed`),

  // requests
  requests: (p: { status?: string; mine?: boolean; page: number; pageSize: number }) =>
    d(api.get<Paged<IssueRequestDto>>(`${base}/requests`, { params: p })),
  request: (id: number) => d(api.get<IssueRequestDto>(`${base}/requests/${id}`)),
  saveRequest: (id: number | null, b: SaveIssueRequest) =>
    d(id ? api.put<IssueRequestDto>(`${base}/requests/${id}`, b) : api.post<IssueRequestDto>(`${base}/requests`, b)),
  submitRequest: (id: number) => d(api.post<IssueRequestDto>(`${base}/requests/${id}/submit`)),
  cancelRequest: (id: number) => d(api.post<IssueRequestDto>(`${base}/requests/${id}/cancel`)),
  fulfil: (id: number, b: { lines: { lineId: number; quantity: number; assetIds: number[] | null }[]; receivedByName: string | null; rowVersion: string }) =>
    d(api.post<IssueRequestDto>(`${base}/requests/${id}/fulfil`, b)),
  suspend: (id: number, reason: string) => d(api.post<IssueRequestDto>(`${base}/requests/${id}/suspend`, { reason })),
  runQuotas: (year: number, month: number) => d(api.post<{ created: number; skipped: number }>(`${base}/requests/quotas/run`, null, { params: { year, month } })),

  // counts
  counts: () => d(api.get<CountCycleDto[]>(`${base}/counts`)),
  count: (id: number) => d(api.get<CountCycleDto>(`${base}/counts/${id}`)),
  saveCount: (id: number | null, b: SaveCountCycle) =>
    d(id ? api.put<CountCycleDto>(`${base}/counts/${id}`, b) : api.post<CountCycleDto>(`${base}/counts`, b)),
  startCount: (id: number) => d(api.post<CountCycleDto>(`${base}/counts/${id}/start`)),
  sheet: (id: number) => d(api.get<SheetLineDto[]>(`${base}/counts/${id}/sheet`)),
  entries: (id: number) => d(api.get<CountEntryDto[]>(`${base}/counts/${id}/entries`)),
  enter: (id: number, b: { itemId: number | null; warehouseId: number | null; quantity: number | null; barcode: string | null; condition: string | null; note: string | null }) =>
    d(api.post<CountEntryDto>(`${base}/counts/${id}/entries`, b)),
  deleteEntry: (id: number, entryId: number) => api.delete(`${base}/counts/${id}/entries/${entryId}`),
  reconcile: (id: number) => d(api.post<VarianceDto[]>(`${base}/counts/${id}/reconcile`)),
  variances: (id: number) => d(api.get<VarianceDto[]>(`${base}/counts/${id}/variances`)),
  justify: (id: number, varianceId: number, justification: string) =>
    d(api.put<VarianceDto>(`${base}/counts/${id}/variances/${varianceId}`, { justification })),
  settle: (id: number) => d(api.post<CountCycleDto>(`${base}/counts/${id}/settle`)),
  cancelCount: (id: number) => d(api.post<CountCycleDto>(`${base}/counts/${id}/cancel`)),

  // maintenance
  workOrders: (p: { folder: string; search?: string; page: number; pageSize: number }) =>
    d(api.get<Paged<WorkOrderDto>>(`${base}/maintenance`, { params: p })),
  workOrderCounts: () => d(api.get<{ open: number; mine: number; closed: number }>(`${base}/maintenance/counts`)),
  reportFault: (b: { barcode: string | null; location: string | null; siteId: number | null; description: string }) =>
    d(api.post<WorkOrderDto>(`${base}/maintenance`, b)),
  updateWorkOrder: (id: number, b: { assigneeEmployeeId: number | null; status: string; assessment: string | null; rowVersion: string }) =>
    d(api.put<WorkOrderDto>(`${base}/maintenance/${id}`, b)),
  issueParts: (id: number, b: { warehouseId: number; lines: { itemId: number; quantity: number }[]; rowVersion: string }) =>
    d(api.post<WorkOrderDto>(`${base}/maintenance/${id}/parts`, b)),
  closeWorkOrder: (id: number, b: { repaired: boolean; laborHours: number | null; laborCost: number; outcome: string; rowVersion: string }) =>
    d(api.post<WorkOrderDto>(`${base}/maintenance/${id}/close`, b)),

  // opening
  opening: () => d(api.get<OpeningStatusDto>(`${base}/opening`)),
  openingTemplate: () => api.get<Blob>(`${base}/opening/template`, { responseType: 'blob' }).then((r) => r.data),
  uploadOpening: (b: { fileName: string; stage: string; content: string }) => d(api.post<OpeningImportDto>(`${base}/opening/upload`, b)),
  lockOpening: () => d(api.post<OpeningStatusDto>(`${base}/opening/lock`)),
}

/** Lines of a document that move pieces need one line per piece; quantity items one line per item. */
export const isAssetKind = (k: ItemKind | string | undefined) => k === 'FixedAsset'

export const num = (v: number | null | undefined) =>
  v == null ? '—' : new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 }).format(v)

export const money = (v: number | null | undefined) =>
  v == null ? '—' : `${new Intl.NumberFormat('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(v)} ₪`
