import { api } from './api'

export interface ExpiringDocumentDto {
  documentId: number
  documentNumber: string
  title: string
  expiryDate: string
  daysRemaining: number
}

export interface DisposalRequestDto {
  id: number
  documentId: number
  documentNumber: string
  documentTitle: string
  action: string
  status: string
  requestedByUserId: number
  requestedAt: string
  approvedByUserId: number | null
  approvedAt: string | null
  executedAt: string | null
  justification: string | null
}

export const lifecycle = {
  expiring: (withinDays = 30) =>
    api.get<ExpiringDocumentDto[]>('/lifecycle/expiring', { params: { withinDays } }).then((r) => r.data),

  disposalRequests: () =>
    api.get<DisposalRequestDto[]>('/lifecycle/disposal-requests').then((r) => r.data),

  requestDisposal: (documentId: number, action: number, justification?: string | null) =>
    api.post<DisposalRequestDto>('/lifecycle/disposal-requests', { documentId, action, justification: justification ?? null }).then((r) => r.data),

  decide: (id: number, approve: boolean, note?: string | null) =>
    api.post<DisposalRequestDto>(`/lifecycle/disposal-requests/${id}/decision`, { approve, note: note ?? null }).then((r) => r.data),

  execute: (id: number) =>
    api.post<DisposalRequestDto>(`/lifecycle/disposal-requests/${id}/execute`, {}).then((r) => r.data),
}

export const DISPOSAL_STATUS_LABELS: Record<string, string> = {
  Pending: 'معلّق', Approved: 'معتمد', Rejected: 'مرفوض', Executed: 'مُنفّذ',
}
export const DISPOSAL_ACTION_LABELS: Record<string, string> = {
  Destroy: 'إتلاف', Transfer: 'ترحيل', Review: 'مراجعة', Retain: 'استبقاء',
}
