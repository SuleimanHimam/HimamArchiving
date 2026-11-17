import { AxiosError } from 'axios'
import { api } from './api'

// Mirrors the HR / core contracts in Archiving.Application (Features/Core, Features/Hr).
// rowVersion is base64: required on every update, ignored on create.

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export type EmployeeCategory = 'Military' | 'Civil' | 'Contract'
export type EmployeeStatus = 'Active' | 'Suspended' | 'Terminated'

export interface EmployeeDto {
  id: number; employeeNumber: string; fullName: string; fullNameEn: string | null
  category: EmployeeCategory; status: EmployeeStatus
  orgUnitId: number; orgUnitName: string | null; positionId: number | null; jobTitle: string
  siteId: number | null; siteName: string | null; hireDate: string | null; userId: number | null; rowVersion: string
}

export interface SaveEmployee {
  employeeNumber: string; fullName: string; fullNameEn: string | null; category: EmployeeCategory
  orgUnitId: number; positionId: number | null; jobTitle: string; siteId: number | null
  hireDate: string | null; userId: number | null; rowVersion?: string | null
}

export interface SiteDto { id: number; name: string; nameEn: string | null; code: string; isActive: boolean; rowVersion: string }

export const LOOKUP_CATEGORIES = [
  'marital_status', 'appointment_type', 'grade', 'qualification_type', 'relationship', 'clearance_party',
] as const
export type LookupCategory = (typeof LOOKUP_CATEGORIES)[number]

export interface LookupDto {
  id: number; category: LookupCategory; code: string; name: string; nameEn: string | null
  sortOrder: number; isActive: boolean; rowVersion: string
}
export type SaveLookup = Omit<LookupDto, 'id' | 'rowVersion'> & { rowVersion?: string | null }

export const ACTION_EFFECTS = [
  'Hire', 'Confirm', 'Promote', 'Transfer', 'Second', 'Allowance', 'Penalty', 'Suspend', 'EndService',
] as const
export type ActionEffect = (typeof ACTION_EFFECTS)[number]

export interface ActionTypeDto {
  id: number; code: string; name: string; effect: ActionEffect; workflowDefinitionId: number | null
  workflowDefinitionName: string | null; isActive: boolean; rowVersion: string
}
export interface SaveActionType {
  code: string; name: string; effect: ActionEffect; workflowDefinitionId: number | null; isActive: boolean
  rowVersion?: string | null
}

export interface ProfileDto {
  birthDate: string | null; maritalStatusId: number | null; address: string | null
  appointmentTypeId: number | null; gradeId: number | null
  nationalIdMasked: string | null; mobileMasked: string | null; rowVersion: string | null
}
/** nationalId / mobile: null keeps the stored value, '' clears it. */
export interface SaveProfile {
  birthDate: string | null; maritalStatusId: number | null; address: string | null
  appointmentTypeId: number | null; gradeId: number | null
  nationalId: string | null; mobile: string | null; rowVersion: string | null
}

export interface FamilyMemberDto { id: number; fullName: string; relationshipId: number; birthDate: string | null; notes: string | null; rowVersion: string }
export type SaveFamilyMember = Omit<FamilyMemberDto, 'id' | 'rowVersion'> & { rowVersion?: string | null }

export interface QualificationDto { id: number; qualificationTypeId: number; title: string; institution: string | null; year: number | null; rowVersion: string }
export type SaveQualification = Omit<QualificationDto, 'id' | 'rowVersion'> & { rowVersion?: string | null }

export interface BankAccountDto { id: number; bankName: string; accountMasked: string; isPrimary: boolean; rowVersion: string }
/** account: required on create; null keeps the stored value on update. */
export interface SaveBankAccount { bankName: string; account: string | null; isPrimary: boolean; rowVersion?: string | null }

export type SensitiveField = 'nationalId' | 'mobile' | 'bankAccount'

export interface ActionPayload {
  employeeNumber?: string | null; fullName?: string | null; fullNameEn?: string | null; category?: EmployeeCategory | null
  orgUnitId?: number | null; positionId?: number | null; jobTitle?: string | null; siteId?: number | null
  gradeId?: number | null; appointmentTypeId?: number | null; toDate?: string | null; notes?: string | null
}

export type ActionStatus = 'Draft' | 'Submitted' | 'Executed' | 'Rejected' | 'Returned' | 'Cancelled'

export interface PersonnelActionDto {
  id: number; number: string | null; actionTypeId: number; actionTypeName: string; effect: ActionEffect
  employeeId: number | null; employeeName: string | null; status: ActionStatus; effectiveDate: string
  reason: string; payload: ActionPayload; beforeJson: string | null; afterJson: string | null
  approvalRequestId: number | null; executedAt: string | null; decisionNote: string | null
  createdAt: string; rowVersion: string
}
export interface SavePersonnelAction {
  actionTypeId: number; employeeId: number | null; effectiveDate: string; reason: string
  payload: ActionPayload; rowVersion?: string | null
}

export interface EmployeeFileDto {
  employee: EmployeeDto; profile: ProfileDto; family: FamilyMemberDto[]
  qualifications: QualificationDto[]; bankAccounts: BankAccountDto[]; actions: PersonnelActionDto[]
}

export interface ClearanceItemDto { id: number; partyId: number; partyName: string; isCleared: boolean; clearedAt: string | null; note: string | null }
export interface ClearanceDto {
  id: number; number: string; employeeId: number; employeeName: string | null
  status: 'Open' | 'Issued'; issuedAt: string | null; items: ClearanceItemDto[]
}

export interface ApprovalRequestDto {
  id: number; number: string; requestType: string; sourceModule: string; sourceId: number; title: string
  status: string; workflowInstanceId: number | null; createdAt: string; decidedAt: string | null; decisionNote: string | null
}

const d = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data)

export const hrApi = {
  // core register
  employees: (q: { search?: string; orgUnitId?: number; status?: string; category?: string; page?: number; pageSize?: number }) =>
    d(api.get<Paged<EmployeeDto>>('/core/employees', { params: q })),
  createEmployee: (b: SaveEmployee) => d(api.post<EmployeeDto>('/core/employees', b)),
  sites: () => d(api.get<SiteDto[]>('/core/sites')),

  // reference lists
  lookups: (category?: LookupCategory, activeOnly = false) =>
    d(api.get<LookupDto[]>('/core/lookups', { params: { category, activeOnly } })),
  createLookup: (b: SaveLookup) => d(api.post<LookupDto>('/core/lookups', b)),
  updateLookup: (id: number, b: SaveLookup) => d(api.put<LookupDto>(`/core/lookups/${id}`, b)),
  deleteLookup: (id: number) => api.delete(`/core/lookups/${id}`),

  // action types
  actionTypes: (activeOnly = false) => d(api.get<ActionTypeDto[]>('/hr/settings/action-types', { params: { activeOnly } })),
  createActionType: (b: SaveActionType) => d(api.post<ActionTypeDto>('/hr/settings/action-types', b)),
  updateActionType: (id: number, b: SaveActionType) => d(api.put<ActionTypeDto>(`/hr/settings/action-types/${id}`, b)),

  // personnel file
  file: (employeeId: number) => d(api.get<EmployeeFileDto>(`/hr/employees/${employeeId}/file`)),
  saveProfile: (employeeId: number, b: SaveProfile) => d(api.put<ProfileDto>(`/hr/employees/${employeeId}/profile`, b)),
  reveal: (employeeId: number, field: SensitiveField, recordId?: number) =>
    d(api.post<{ value: string }>(`/hr/employees/${employeeId}/reveal`, { field, recordId })).then((r) => r.value),
  addFamily: (e: number, b: SaveFamilyMember) => d(api.post<FamilyMemberDto>(`/hr/employees/${e}/family`, b)),
  updateFamily: (e: number, id: number, b: SaveFamilyMember) => d(api.put<FamilyMemberDto>(`/hr/employees/${e}/family/${id}`, b)),
  deleteFamily: (e: number, id: number) => api.delete(`/hr/employees/${e}/family/${id}`),
  addQualification: (e: number, b: SaveQualification) => d(api.post<QualificationDto>(`/hr/employees/${e}/qualifications`, b)),
  updateQualification: (e: number, id: number, b: SaveQualification) =>
    d(api.put<QualificationDto>(`/hr/employees/${e}/qualifications/${id}`, b)),
  deleteQualification: (e: number, id: number) => api.delete(`/hr/employees/${e}/qualifications/${id}`),
  addBank: (e: number, b: SaveBankAccount) => d(api.post<BankAccountDto>(`/hr/employees/${e}/bank-accounts`, b)),
  updateBank: (e: number, id: number, b: SaveBankAccount) => d(api.put<BankAccountDto>(`/hr/employees/${e}/bank-accounts/${id}`, b)),
  deleteBank: (e: number, id: number) => api.delete(`/hr/employees/${e}/bank-accounts/${id}`),

  // personnel actions
  actions: (q: { employeeId?: number; status?: string; page?: number; pageSize?: number }) =>
    d(api.get<Paged<PersonnelActionDto>>('/hr/actions', { params: q })),
  createAction: (b: SavePersonnelAction) => d(api.post<PersonnelActionDto>('/hr/actions', b)),
  updateAction: (id: number, b: SavePersonnelAction) => d(api.put<PersonnelActionDto>(`/hr/actions/${id}`, b)),
  submitAction: (id: number) => d(api.post<PersonnelActionDto>(`/hr/actions/${id}/submit`)),
  cancelAction: (id: number) => d(api.post<PersonnelActionDto>(`/hr/actions/${id}/cancel`)),

  // clearance
  clearances: (status?: string) => d(api.get<ClearanceDto[]>('/hr/clearances', { params: { status } })),
  clearItem: (id: number, itemId: number, note: string | null) =>
    d(api.post<ClearanceDto>(`/hr/clearances/${id}/items/${itemId}/clear`, { note })),

  myRequests: () => d(api.get<ApprovalRequestDto[]>('/core/approvals/mine')),
}

/** The server's own message when it sent one; the caller's fallback otherwise. Never a stack trace. */
export const apiError = (e: unknown, fallback: string) =>
  (e as AxiosError<{ error?: string }>).response?.data?.error ?? fallback
