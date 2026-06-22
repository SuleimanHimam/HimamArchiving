import { api } from './api'
import type { PagedResult } from './incomingMail'

export interface EligibilityDto { documentId: number; eligible: boolean; reasons: string[] }

export interface LegalHold {
  id: number; reason: string; scope: number
  documentId: number | null; folderId: number | null; orgUnitId: number | null
  placedByUserId: number; placedByName: string | null; placedAtUtc: string
  releasedByUserId: number | null; releasedAtUtc: string | null; isActive: boolean
}

export interface DestructionItem {
  id: number; documentId: number; documentNumber: string; documentTitle: string
  method: number; customMethod: string | null; checksumBefore: string | null; outcome: string | null
}

export interface DestructionRequest {
  id: number; status: string; reason: string; retentionBasisId: number | null
  requestedByUserId: number; requestedByName: string | null; requestedAtUtc: string
  approvedByUserId: number | null; approvedByName: string | null; approvedAtUtc: string | null
  executedByUserId: number | null; executedAtUtc: string | null
  decisionNote: string | null; scheduledForUtc: string | null; certificateId: number | null
  items: DestructionItem[]
}

export const METHOD_LABELS: Record<number, string> = {
  0: 'محو التشفير (Crypto-Shred)', 1: 'كتابة آمنة فوقية', 2: 'حذف + إبطال البصمة',
  3: 'تقطيع', 4: 'حرق', 5: 'تذويب', 6: 'إزالة مغناطيسية', 7: 'أخرى (حدّد الطريقة)',
}
export const METHOD_OTHER = 7

export const DESTRUCTION_STATUS_LABELS: Record<string, string> = {
  Draft: 'مسودة', PendingReview: 'قيد المراجعة', PendingApproval: 'بانتظار الاعتماد',
  Approved: 'معتمد', Rejected: 'مرفوض', Scheduled: 'مجدول', Executing: 'قيد التنفيذ',
  Completed: 'منفّذ', Cancelled: 'ملغى', Failed: 'فشل',
}

export const HOLD_SCOPE_LABELS: Record<number, string> = { 0: 'وثيقة', 1: 'مجلد', 2: 'وحدة تنظيمية', 3: 'استعلام' }

export const destruction = {
  eligibility: (documentId: number) =>
    api.get<EligibilityDto>(`/destruction/eligibility/${documentId}`).then((r) => r.data),
  list: (status?: number) =>
    api.get<PagedResult<DestructionRequest>>('/destruction/requests', { params: { status, pageSize: 50 } }).then((r) => r.data),
  get: (id: number) => api.get<DestructionRequest>(`/destruction/requests/${id}`).then((r) => r.data),
  create: (b: { documentIds: number[]; reason: string; method: number; customMethod?: string | null; scheduledForUtc?: string | null }) =>
    api.post<DestructionRequest>('/destruction/requests', b).then((r) => r.data),
  submit: (id: number) => api.post<DestructionRequest>(`/destruction/requests/${id}/submit`, {}).then((r) => r.data),
  approve: (id: number, note?: string | null) => api.post<DestructionRequest>(`/destruction/requests/${id}/approve`, { note }).then((r) => r.data),
  reject: (id: number, note?: string | null) => api.post<DestructionRequest>(`/destruction/requests/${id}/reject`, { note }).then((r) => r.data),
  cancel: (id: number) => api.post<DestructionRequest>(`/destruction/requests/${id}/cancel`, {}).then((r) => r.data),
  execute: (id: number, b: { stepUpPassword: string; physicalOfficer?: string | null; physicalWitness?: string | null }) =>
    api.post<DestructionRequest>(`/destruction/requests/${id}/execute`, b).then((r) => r.data),
  downloadCertificate: async (id: number) => {
    const res = await api.get(`/destruction/requests/${id}/certificate`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data as Blob)
    const a = document.createElement('a')
    a.href = url; a.download = `certificate-${id}.pdf`
    document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url)
  },
}

export interface DestructionMethodOption { id: number; label: string; sortOrder: number; isActive: boolean }

export const destructionMethods = {
  list: () => api.get<DestructionMethodOption[]>('/destruction/methods').then((r) => r.data),
  create: (b: { label: string; isActive: boolean }) => api.post<DestructionMethodOption>('/destruction/methods', b).then((r) => r.data),
  update: (id: number, b: { label: string; isActive: boolean }) => api.put<DestructionMethodOption>(`/destruction/methods/${id}`, b).then((r) => r.data),
  remove: (id: number) => api.delete(`/destruction/methods/${id}`),
}

export const legalHolds = {
  list: (activeOnly = false) => api.get<LegalHold[]>('/legal-holds', { params: { activeOnly } }).then((r) => r.data),
  place: (b: { reason: string; scope: number; documentId?: number | null; folderId?: number | null; orgUnitId?: number | null }) =>
    api.post<LegalHold>('/legal-holds', b).then((r) => r.data),
  release: (id: number) => api.post(`/legal-holds/${id}/release`, {}),
}
