import { api } from './api'
import type { PagedResult } from './shared'

export interface ExternalOrg {
  id: number
  name: string
  nameEn: string | null
  type: string
  code: string | null
  isActive: boolean
  notes: string | null
  contactCount: number
  /** Logo as a data URI, or null when the organisation has none. */
  logoUrl: string | null
}

export interface ContactChannel { id?: number; type: string | number; value: string; isPrimary: boolean }

export interface ExternalContact {
  id: number
  organizationId: number | null
  organizationName: string | null
  name: string
  nameEn: string | null
  title: string | null
  isActive: boolean
  channels: ContactChannel[]
}

export interface OrgInput {
  name: string; nameEn?: string | null; type: number; code?: string | null; notes?: string | null
  /** A `data:image/…` URI, or null to clear the logo. */
  logoUrl?: string | null
}
export interface ContactInput {
  organizationId?: number | null
  name: string
  nameEn?: string | null
  title?: string | null
  channels?: ContactChannel[]
}

export const ORG_TYPES = ['Government', 'Ministry', 'Company', 'Authority', 'Ngo', 'Individual', 'Other']
export const CHANNEL_TYPES = ['Email', 'Phone', 'Mobile', 'Fax', 'Address']
export const ORG_TYPE_AR: Record<string, string> = {
  Government: 'جهة حكومية', Ministry: 'وزارة', Company: 'شركة', Authority: 'هيئة',
  Ngo: 'مؤسسة أهلية', Individual: 'فرد', Other: 'أخرى',
}
export const CHANNEL_TYPE_AR: Record<string, string> = {
  Email: 'بريد إلكتروني', Phone: 'هاتف', Mobile: 'جوال', Fax: 'فاكس', Address: 'عنوان',
}

export const external = {
  orgs: (search?: string, page = 1, pageSize = 50) =>
    api.get<PagedResult<ExternalOrg>>('/external/organizations', { params: { search, page, pageSize } }).then((r) => r.data),
  createOrg: (body: OrgInput) => api.post<ExternalOrg>('/external/organizations', body).then((r) => r.data),
  updateOrg: (id: number, body: OrgInput) => api.put<ExternalOrg>(`/external/organizations/${id}`, body).then((r) => r.data),
  deleteOrg: (id: number) => api.delete(`/external/organizations/${id}`),

  contacts: (organizationId?: number | null, search?: string, page = 1, pageSize = 50) =>
    api.get<PagedResult<ExternalContact>>('/external/contacts', { params: { organizationId, search, page, pageSize } }).then((r) => r.data),
  createContact: (body: ContactInput) => api.post<ExternalContact>('/external/contacts', body).then((r) => r.data),
  updateContact: (id: number, body: ContactInput) => api.put<ExternalContact>(`/external/contacts/${id}`, body).then((r) => r.data),
  deleteContact: (id: number) => api.delete(`/external/contacts/${id}`),
}
