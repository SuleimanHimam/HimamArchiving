import { api } from './api'
import type { EmployeeCategory, EmployeeDto, Paged } from './hr'

/* HR phases 2–3 client: calendar, leave, shifts, attendance, payroll. Enum values travel as names. */

const d = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data)

// ---- calendar ----
export interface HrSettingsDto { weekendDays: string; defaultShiftTypeId: number | null; absenceLeaveTypeId: number | null }
export interface HolidayDto { id: number; date: string; name: string; rowVersion: string }

// ---- leave ----
export const LEAVE_KINDS = ['Leave', 'Departure', 'Mission'] as const
export type LeaveKind = (typeof LEAVE_KINDS)[number]
export type LeaveStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Returned' | 'Cancelled'

export interface LeaveTypeDto {
  id: number; code: string; name: string; kind: LeaveKind; unit: 'Days' | 'Hours'; deductsBalance: boolean
  countWorkingDaysOnly: boolean; requiresSubstitute: boolean; annualEntitlement: number; maxCarryOver: number | null
  appliesToMilitary: boolean; appliesToCivil: boolean; appliesToContract: boolean; workflowDefinitionId: number | null
  workflowDefinitionName: string | null; isActive: boolean; rowVersion: string
}
export type SaveLeaveType = Omit<LeaveTypeDto, 'id' | 'workflowDefinitionName' | 'rowVersion'> & { rowVersion?: string | null }

export interface LeaveRequestDto {
  id: number; number: string | null; employeeId: number; employeeName: string | null; leaveTypeId: number; leaveTypeName: string
  kind: LeaveKind; unit: 'Days' | 'Hours'; fromDate: string; toDate: string; fromTime: string | null; toTime: string | null
  quantity: number; substituteEmployeeId: number | null; substituteName: string | null; reason: string; status: LeaveStatus
  approvalRequestId: number | null; decidedAt: string | null; decisionNote: string | null; createdAt: string; rowVersion: string
}
export interface SaveLeave {
  employeeId: number | null; leaveTypeId: number; fromDate: string; toDate: string; fromTime: string | null; toTime: string | null
  substituteEmployeeId: number | null; reason: string; rowVersion?: string | null
}
export interface LeaveCheckDto { quantity: number; balance: number | null; balanceAfter: number | null; sufficient: boolean; problems: string[] }
export interface LeaveBalanceDto {
  leaveTypeId: number; leaveTypeName: string; unit: string; year: number; entitled: number; taken: number; pending: number; balance: number
}
export interface LeaveMovementDto {
  id: number; leaveTypeId: number; leaveTypeName: string; year: number; quantity: number; kind: string
  sourceType: string; sourceId: number | null; note: string | null; createdAt: string
}

// ---- shifts ----
export interface ShiftTypeDto {
  id: number; code: string; name: string; startTime: string; endTime: string; graceMinutes: number; isNight: boolean
  isActive: boolean; rowVersion: string
}
export type SaveShiftType = Omit<ShiftTypeDto, 'id' | 'rowVersion'> & { rowVersion?: string | null }
export interface CoverageRuleDto { id: number; siteId: number; shiftTypeId: number; minCount: number }
export type CellKind = 'Shift' | 'Rest' | 'Leave'
export interface RosterCellDto { employeeId: number; date: string; kind: CellKind; shiftTypeId: number | null }
export interface RosterDto {
  siteId: number; year: number; month: number; status: 'Draft' | 'Published'; publishedAt: string | null
  employees: { id: number; employeeNumber: string; fullName: string }[]; cells: RosterCellDto[]
  coverage: { date: string; shiftTypeId: number; count: number; minCount: number }[]
}
export interface RosterChange { employeeId: number; date: string; kind: 'Shift' | 'Rest' | 'Clear'; shiftTypeId: number | null }

// ---- attendance ----
export interface AttendanceExceptionDto {
  id: number; employeeId: number; employeeName: string; date: string; kind: 'Late' | 'EarlyLeave' | 'MissingPunch' | 'Absent'
  minutes: number; status: 'Pending' | 'Resolved'; resolution: string | null; resolutionNote: string | null; resolvedAt: string | null
}
export interface AttendanceDayDto {
  employeeId: number; employeeNumber: string; employeeName: string; date: string; shiftName: string | null
  firstIn: string | null; lastOut: string | null; sources: string[]; state: string; exceptions: AttendanceExceptionDto[]
}
export interface AttendancePeriodDto { year: number; month: number; isClosed: boolean; closedAt: string | null; pendingExceptions: number }
export interface ImportResult { imported: number; duplicates: number; errors: string[] }
export const RESOLUTIONS = ['Justified', 'DeductLeave', 'DeductSalary', 'PunchCorrected'] as const

// ---- payroll ----
export const PAY_METHODS = ['Basic', 'Fixed', 'PercentOfBasic', 'PerUnit', 'Tax'] as const
export const PAY_SOURCES = ['Manual', 'NightShifts', 'AbsenceMinutes'] as const
export const PAYROLL_STAGES = ['Open', 'Inputs', 'Calculated', 'Reviewed', 'Audited', 'Approved', 'Disbursed', 'Closed'] as const
export type PayrollStage = (typeof PAYROLL_STAGES)[number]

export interface PayElementDto {
  id: number; code: string; name: string; kind: 'Earning' | 'Deduction'; method: (typeof PAY_METHODS)[number]
  source: (typeof PAY_SOURCES)[number]; isTaxable: boolean; isPreTax: boolean; appliesToMilitary: boolean; appliesToCivil: boolean
  appliesToContract: boolean; sortOrder: number; isActive: boolean; rowVersion: string
}
export type SavePayElement = Omit<PayElementDto, 'id' | 'rowVersion'> & { rowVersion?: string | null }
export interface PayRateDto { id: number; payElementId: number; category: EmployeeCategory | null; effectiveFrom: string; value: number }
export interface ScaleRateDto { id: number; gradeId: number; gradeName: string; category: EmployeeCategory | null; effectiveFrom: string; basicAmount: number }
export interface EmployeePayDto { id: number; employeeId: number; payElementId: number; payElementName: string; amount: number; fromDate: string; toDate: string | null }
export interface TaxBracketDto { id: number; effectiveFrom: string; fromAmount: number; toAmount: number | null; rate: number }

export interface PayrollPeriodDto {
  id: number; year: number; month: number; stage: PayrollStage; employees: number; gross: number; deductions: number; net: number
  paid: number | null; log: { stage: PayrollStage; userName: string | null; at: string; isReturn: boolean; note: string | null }[]
  rowVersion: string
}
export interface PayrollInputDto { id: number; employeeId: number; employeeName: string; payElementId: number; payElementName: string; quantity: number; note: string | null }
export interface PayrollResultDto {
  employeeId: number; employeeNumber: string; employeeName: string; category: EmployeeCategory; gross: number; deductions: number
  net: number; paid: number | null; carried: number | null; previousNet: number | null; difference: number | null
}
export interface PayslipDto {
  periodId: number; year: number; month: number; stage: PayrollStage; employeeId: number; employeeNumber: string; employeeName: string
  category: EmployeeCategory; lines: { code: string; name: string; kind: string; quantity: number; rate: number; amount: number }[]
  gross: number; deductions: number; net: number; paid: number | null; carried: number | null; entitlementBalance: number
}
export interface DisbursementRule { category: EmployeeCategory; percentOfSalary: number; minimumAmount: number; percentOfAccumulated: number }
export interface DisbursementSimulation {
  categories: { category: EmployeeCategory; employees: number; net: number; paid: number; carried: number }[]
  totalPaid: number; totalCarried: number; availableLiquidity: number | null; withinLiquidity: boolean
  employees: { employeeId: number; employeeName: string; category: EmployeeCategory; net: number; accumulatedBefore: number; paid: number; balanceAfter: number }[]
}
export interface EntitlementStatement {
  employeeId: number; balance: number
  movements: { id: number; year: number | null; month: number | null; kind: string; amount: number; note: string | null; createdAt: string }[]
}

export const hrTime = {
  // calendar
  settings: () => d(api.get<HrSettingsDto>('/hr/calendar/settings')),
  saveSettings: (b: HrSettingsDto) => d(api.put<HrSettingsDto>('/hr/calendar/settings', b)),
  holidays: (year: number) => d(api.get<HolidayDto[]>('/hr/calendar/holidays', { params: { year } })),
  addHoliday: (b: { date: string; name: string }) => d(api.post<HolidayDto>('/hr/calendar/holidays', b)),
  deleteHoliday: (id: number) => api.delete(`/hr/calendar/holidays/${id}`),
  manager: (employeeId: number) => d(api.get<EmployeeDto | null>(`/hr/calendar/manager/${employeeId}`)),

  // leave
  leaveTypes: (activeOnly = false) => d(api.get<LeaveTypeDto[]>('/hr/leave-types', { params: { activeOnly } })),
  createLeaveType: (b: SaveLeaveType) => d(api.post<LeaveTypeDto>('/hr/leave-types', b)),
  updateLeaveType: (id: number, b: SaveLeaveType) => d(api.put<LeaveTypeDto>(`/hr/leave-types/${id}`, b)),
  leaves: (q: { employeeId?: number; status?: string; page?: number; pageSize?: number }) =>
    d(api.get<Paged<LeaveRequestDto>>('/hr/leave', { params: q })),
  myLeaves: () => d(api.get<LeaveRequestDto[]>('/hr/leave/mine')),
  checkLeave: (b: SaveLeave, excludeId?: number) => d(api.post<LeaveCheckDto>('/hr/leave/check', b, { params: { excludeId } })),
  createLeave: (b: SaveLeave) => d(api.post<LeaveRequestDto>('/hr/leave', b)),
  updateLeave: (id: number, b: SaveLeave) => d(api.put<LeaveRequestDto>(`/hr/leave/${id}`, b)),
  submitLeave: (id: number) => d(api.post<LeaveRequestDto>(`/hr/leave/${id}/submit`)),
  cancelLeave: (id: number) => d(api.post<LeaveRequestDto>(`/hr/leave/${id}/cancel`)),
  myBalances: (year: number) => d(api.get<LeaveBalanceDto[]>('/hr/leave/balances/mine', { params: { year } })),
  balances: (employeeId: number, year: number) => d(api.get<LeaveBalanceDto[]>(`/hr/leave/balances/${employeeId}`, { params: { year } })),
  movements: (employeeId: number, year: number) => d(api.get<LeaveMovementDto[]>(`/hr/leave/movements/${employeeId}`, { params: { year } })),
  adjust: (b: { employeeId: number; leaveTypeId: number; year: number; quantity: number; kind: 'Opening' | 'Adjustment'; note: string }) =>
    d(api.post<LeaveMovementDto>('/hr/leave/adjustments', b)),
  grantYear: (year: number) => d(api.post<{ granted: number; carriedOver: number; skipped: number }>(`/hr/leave/grant/${year}`)),

  // shifts
  shiftTypes: (activeOnly = false) => d(api.get<ShiftTypeDto[]>('/hr/shifts/types', { params: { activeOnly } })),
  createShiftType: (b: SaveShiftType) => d(api.post<ShiftTypeDto>('/hr/shifts/types', b)),
  updateShiftType: (id: number, b: SaveShiftType) => d(api.put<ShiftTypeDto>(`/hr/shifts/types/${id}`, b)),
  coverage: (siteId: number) => d(api.get<CoverageRuleDto[]>(`/hr/shifts/coverage/${siteId}`)),
  saveCoverage: (b: { siteId: number; shiftTypeId: number; minCount: number }) => d(api.put<CoverageRuleDto>('/hr/shifts/coverage', b)),
  roster: (siteId: number, y: number, m: number) => d(api.get<RosterDto>(`/hr/shifts/roster/${siteId}/${y}/${m}`)),
  saveCells: (siteId: number, y: number, m: number, cells: RosterChange[]) =>
    d(api.put<RosterDto>(`/hr/shifts/roster/${siteId}/${y}/${m}`, { cells })),
  publish: (siteId: number, y: number, m: number) => d(api.post<RosterDto>(`/hr/shifts/roster/${siteId}/${y}/${m}/publish`)),

  // attendance
  importPunches: (source: 'Device' | 'Import', lines: { employeeNumber: string; punchedAt: string; deviceCode: string | null }[]) =>
    d(api.post<ImportResult>('/hr/attendance/punches/import', { source, lines })),
  process: (from: string, to: string) => d(api.post<{ days: number; exceptions: number }>('/hr/attendance/process', { from, to })),
  day: (date: string, siteId?: number) => d(api.get<AttendanceDayDto[]>('/hr/attendance/day', { params: { date, siteId } })),
  myAttendance: (y: number, m: number) => d(api.get<AttendanceDayDto[]>(`/hr/attendance/mine/${y}/${m}`)),
  exceptions: (q: { status?: string; from?: string; to?: string }) => d(api.get<AttendanceExceptionDto[]>('/hr/attendance/exceptions', { params: q })),
  resolve: (id: number, b: { resolution: string; note: string; correctionTime?: string | null }) =>
    d(api.post<AttendanceExceptionDto>(`/hr/attendance/exceptions/${id}/resolve`, b)),
  period: (y: number, m: number) => d(api.get<AttendancePeriodDto>(`/hr/attendance/periods/${y}/${m}`)),
  closePeriod: (y: number, m: number) => d(api.post<AttendancePeriodDto>(`/hr/attendance/periods/${y}/${m}/close`)),

  // payroll settings
  elements: (activeOnly = false) => d(api.get<PayElementDto[]>('/hr/payroll-settings/elements', { params: { activeOnly } })),
  createElement: (b: SavePayElement) => d(api.post<PayElementDto>('/hr/payroll-settings/elements', b)),
  updateElement: (id: number, b: SavePayElement) => d(api.put<PayElementDto>(`/hr/payroll-settings/elements/${id}`, b)),
  rates: () => d(api.get<PayRateDto[]>('/hr/payroll-settings/rates')),
  addRate: (b: { payElementId: number; category: string | null; effectiveFrom: string; value: number }) =>
    d(api.post<PayRateDto>('/hr/payroll-settings/rates', b)),
  scale: () => d(api.get<ScaleRateDto[]>('/hr/payroll-settings/scale')),
  addScale: (b: { gradeId: number; category: string | null; effectiveFrom: string; basicAmount: number }) =>
    d(api.post<ScaleRateDto>('/hr/payroll-settings/scale', b)),
  employeePay: (employeeId: number) => d(api.get<EmployeePayDto[]>(`/hr/payroll-settings/employees/${employeeId}`)),
  addEmployeePay: (b: { employeeId: number; payElementId: number; amount: number; fromDate: string; toDate: string | null }) =>
    d(api.post<EmployeePayDto>('/hr/payroll-settings/employees', b)),
  endEmployeePay: (id: number, toDate: string) => d(api.post<EmployeePayDto>(`/hr/payroll-settings/employees/items/${id}/end`, { toDate })),
  tax: () => d(api.get<TaxBracketDto[]>('/hr/payroll-settings/tax')),
  saveTax: (b: { effectiveFrom: string; brackets: { fromAmount: number; toAmount: number | null; rate: number }[] }) =>
    d(api.post<TaxBracketDto[]>('/hr/payroll-settings/tax', b)),

  // payroll month
  periods: () => d(api.get<PayrollPeriodDto[]>('/hr/payroll')),
  openPeriod: (year: number, month: number) => d(api.post<PayrollPeriodDto>('/hr/payroll', { year, month })),
  inputs: (id: number) => d(api.get<PayrollInputDto[]>(`/hr/payroll/${id}/inputs`)),
  addInput: (id: number, b: { employeeId: number; payElementId: number; quantity: number; note: string | null }) =>
    d(api.post<PayrollInputDto>(`/hr/payroll/${id}/inputs`, b)),
  deleteInput: (id: number, inputId: number) => api.delete(`/hr/payroll/${id}/inputs/${inputId}`),
  advance: (id: number, note: string | null) => d(api.post<PayrollPeriodDto>(`/hr/payroll/${id}/advance`, { note })),
  signOff: (id: number, note: string | null) => d(api.post<PayrollPeriodDto>(`/hr/payroll/${id}/sign-off`, { note })),
  recalculate: (id: number) => d(api.post<{ employees: number; warnings: string[]; period: PayrollPeriodDto }>(`/hr/payroll/${id}/calculate`)),
  returnPeriod: (id: number, reason: string) => d(api.post<PayrollPeriodDto>(`/hr/payroll/${id}/return`, { reason })),
  results: (id: number) => d(api.get<PayrollResultDto[]>(`/hr/payroll/${id}/results`)),
  payslip: (id: number, employeeId: number) => d(api.get<PayslipDto>(`/hr/payroll/${id}/payslips/${employeeId}`)),
  disbursementRules: (id: number) => d(api.get<DisbursementRule[]>(`/hr/payroll/${id}/disbursement`)),
  simulate: (id: number, rules: DisbursementRule[], availableLiquidity: number | null) =>
    d(api.post<DisbursementSimulation>(`/hr/payroll/${id}/disbursement/simulate`, { rules, availableLiquidity })),
  disburse: (id: number, rules: DisbursementRule[], availableLiquidity: number | null) =>
    d(api.post<PayrollPeriodDto>(`/hr/payroll/${id}/disbursement`, { rules, availableLiquidity })),
  entitlements: (employeeId: number) => d(api.get<EntitlementStatement>(`/hr/payroll/entitlements/${employeeId}`)),
  myEntitlements: () => d(api.get<EntitlementStatement>('/hr/payroll/entitlements/mine')),
  myPayslips: () => d(api.get<PayslipDto[]>('/hr/payroll/payslips/mine')),
}

/** Shekel amounts with two decimals, grouped. */
export const money = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export const ym = (y: number, m: number) => `${y}-${String(m).padStart(2, '0')}`

/** Days of a month as YYYY-MM-DD. */
export const monthDays = (y: number, m: number) =>
  Array.from({ length: new Date(Date.UTC(y, m, 0)).getUTCDate() }, (_, i) => `${ym(y, m)}-${String(i + 1).padStart(2, '0')}`)
