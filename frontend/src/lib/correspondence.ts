import { api } from './api'

export interface CorrespondenceRecipient {
  orgUnitId: number
  orgUnitName: string | null
  deliveredAtUtc: string
  status: number               // 0 Delivered, 1 Acknowledged, 2 Objected
  statusLabel: string
  acknowledgedByUserId: number | null
  acknowledgedByName: string | null
  acknowledgedAtUtc: string | null
  comment: string | null
}

export interface Correspondence {
  id: number
  reference: string
  eventType: number
  eventTypeLabel: string
  documentId: number
  documentTitle: string | null
  fromOrgUnitId: number
  fromOrgUnitName: string | null
  responsibleOfficerUserId: number | null
  responsibleOfficerName: string | null
  action: number               // 0 ForInfo, 1 Acknowledge, 2 Approve
  actionLabel: string
  responseDeadline: string | null
  subject: string
  body: string
  certificateReference: string | null
  status: number               // 0 Open,1 Acknowledged,2 Overdue,3 Closed,4 Cancelled
  statusLabel: string
  isOverdue: boolean
  createdAt: string
  recipients: CorrespondenceRecipient[]
}

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export const CORR_EVENT_TONE: Record<number, string> = {
  0: 'internal',   // added
  1: 'internal',   // updated
  2: 'internal',   // transferred
  3: 'secret',     // destruction request
  4: 'secret',     // destroyed
}

export const correspondence = {
  list: (box: 'inbox' | 'outbox' | 'all' = 'inbox', page = 1, pageSize = 50) =>
    api.get<Paged<Correspondence>>('/correspondence', { params: { box, page, pageSize } }).then((r) => r.data),
  get: (id: number) => api.get<Correspondence>(`/correspondence/${id}`).then((r) => r.data),
  acknowledge: (id: number, comment?: string | null) =>
    api.post<Correspondence>(`/correspondence/${id}/acknowledge`, { comment }).then((r) => r.data),
  object: (id: number, comment: string) =>
    api.post<Correspondence>(`/correspondence/${id}/object`, { comment }).then((r) => r.data),
}
