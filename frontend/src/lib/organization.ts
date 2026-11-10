import { api } from './api'

// Org-unit kinds mirror the backend OrgUnitType enum.
export const ORG_UNIT_TYPES = [
  { v: 0, ar: 'مؤسسة' },
  { v: 1, ar: 'إدارة / مديرية' },
  { v: 2, ar: 'قسم' },
  { v: 3, ar: 'وحدة' },
  { v: 4, ar: 'لجنة' },
  { v: 5, ar: 'فريق عمل' },
] as const

export const ORG_UNIT_TYPE_LABEL: Record<string, string> = {
  Institution: 'مؤسسة', Directorate: 'إدارة / مديرية', Department: 'قسم',
  Unit: 'وحدة', Committee: 'لجنة', Team: 'فريق عمل',
}

export interface InstitutionDto {
  id: number
  name: string
  nameEn: string | null
  code: string | null
  isActive: boolean
}

export interface OrgUnitDto {
  id: number
  institutionId: number
  parentId: number | null
  name: string
  nameEn: string | null
  code: string | null
  type: string
  sortOrder: number
  isActive: boolean
}

export interface PositionDto {
  id: number
  title: string
  code: string | null
  orgUnitId: number
  orgUnitName: string
  rank: number
  currentOccupantUserId: number | null
  currentOccupantName: string | null
  isActive: boolean
}

export interface UserLookupDto {
  id: number
  fullName: string
  email: string
}

export interface OrgUnitInput {
  institutionId: number
  parentId: number | null
  name: string
  nameEn: string | null
  code: string | null
  type: number
  sortOrder: number
}

export interface PositionInput {
  title: string
  code: string | null
  orgUnitId: number
  rank: number
}

export const organization = {
  institutions: () => api.get<InstitutionDto[]>('/organization/institutions').then((r) => r.data),
  createInstitution: (b: { name: string; nameEn?: string | null; code?: string | null }) =>
    api.post<InstitutionDto>('/organization/institutions', b).then((r) => r.data),

  orgUnits: (institutionId?: number) =>
    api.get<OrgUnitDto[]>('/organization/org-units', { params: { institutionId } }).then((r) => r.data),
  createOrgUnit: (b: OrgUnitInput) =>
    api.post<OrgUnitDto>('/organization/org-units', b).then((r) => r.data),
  updateOrgUnit: (id: number, b: Omit<OrgUnitInput, 'institutionId'> & { isActive: boolean }) =>
    api.put<OrgUnitDto>(`/organization/org-units/${id}`, b).then((r) => r.data),
  deleteOrgUnit: (id: number) => api.delete(`/organization/org-units/${id}`),

  positions: (orgUnitId?: number) =>
    api.get<PositionDto[]>('/organization/positions', { params: { orgUnitId } }).then((r) => r.data),
  createPosition: (b: PositionInput) =>
    api.post<PositionDto>('/organization/positions', b).then((r) => r.data),
  updatePosition: (id: number, b: PositionInput & { isActive: boolean }) =>
    api.put<PositionDto>(`/organization/positions/${id}`, b).then((r) => r.data),
  deletePosition: (id: number) => api.delete(`/organization/positions/${id}`),
  assignOccupant: (id: number, userId: number) =>
    api.post<PositionDto>(`/organization/positions/${id}/occupant`, { userId }).then((r) => r.data),

  users: () => api.get<UserLookupDto[]>('/organization/users').then((r) => r.data),
}
