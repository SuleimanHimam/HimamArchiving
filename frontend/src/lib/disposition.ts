import { api } from './api'

export interface DispositionRequest {
  id: number
  documentId: number
  documentNumber: string | null
  documentTitle: string | null
  requestedAction: number          // 0 Destroy, 1 Renew
  requestedActionLabel: string
  reason: string
  requestedByUserId: number
  requestedByName: string | null
  requestedAtUtc: string
  status: number                   // 0 PendingVerification,1 PendingFinalApproval,2 Approved,3 Rejected,4 Completed
  statusLabel: string
  verifiedByUserId: number | null
  verifiedByName: string | null
  verifiedAtUtc: string | null
  verificationNotes: string | null
  finalApprovedByUserId: number | null
  finalApprovedByName: string | null
  finalApprovedAtUtc: string | null
  finalApprovalNotes: string | null
  rejectedByUserId: number | null
  rejectedByName: string | null
  rejectedAtUtc: string | null
  rejectionReason: string | null
  newExpiryDate: string | null
  method: number
  customMethod: string | null
  certificateId: number | null
  certificateNumber: string | null
  expiryDate: string | null
  boxCode: string | null
}

export interface DispositionCertificate {
  requestId: number
  certificateNumber: string
  documentIds: number[]
  documentNumbers: string[]
  destructionMethod: string
  verifiedByName: string | null
  finalApprovedByName: string | null
  generatedAtUtc: string
}

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export const DISPOSITION_ACTION = { Destroy: 0, Renew: 1 } as const
export const DISPOSITION_STATUS = {
  PendingVerification: 0, PendingFinalApproval: 1, Approved: 2, Rejected: 3, Completed: 4,
} as const

/** Admin-named destruction methods (mirrors the backend DestructionMethod enum order). */
export const DESTRUCTION_METHODS: { value: number; label: string }[] = [
  { value: 0, label: 'محو تشفيري' },
  { value: 1, label: 'كتابة فوقية آمنة' },
  { value: 3, label: 'تقطيع' },
  { value: 4, label: 'حرق' },
  { value: 5, label: 'تذويب' },
  { value: 6, label: 'إزالة مغناطيسية' },
  { value: 7, label: 'أخرى' },
]

export const disposition = {
  list: (stage?: 'Verification' | 'FinalApproval', page = 1, pageSize = 50) =>
    api.get<Paged<DispositionRequest>>('/disposition-requests', { params: { stage, page, pageSize } }).then((r) => r.data),
  get: (id: number) => api.get<DispositionRequest>(`/disposition-requests/${id}`).then((r) => r.data),
  create: (b: { documentId: number; requestedAction: number; reason: string; method?: number; customMethod?: string | null }) =>
    api.post<DispositionRequest>('/disposition-requests', b).then((r) => r.data),
  verify: (id: number, decision: 'Verify' | 'Reject', notes: string) =>
    api.post<DispositionRequest>(`/disposition-requests/${id}/verify`, { decision, notes }).then((r) => r.data),
  finalApprove: (id: number, decision: 'Approve' | 'Reject', notes: string, newExpiryDate?: string | null) =>
    api.post<DispositionRequest>(`/disposition-requests/${id}/final-approve`, { decision, notes, newExpiryDate }).then((r) => r.data),
  reject: (id: number, reason: string) =>
    api.post<DispositionRequest>(`/disposition-requests/${id}/reject`, { reason }).then((r) => r.data),
  certificate: (id: number) =>
    api.get<DispositionCertificate>(`/disposition-requests/${id}/certificate`).then((r) => r.data),
}
