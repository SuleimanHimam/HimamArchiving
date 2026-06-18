import { api } from './api'
import type { PagedResult, Confidentiality, PriorityLevel, TimelineEntry } from './incomingMail'

export interface OutgoingMailListItem {
  id: number
  letterNumber: string
  recipientEntity: string
  subject: string
  confidentiality: string
  priority: string
  status: string
  sentDate: string | null
  createdAt: string
}

export interface OutgoingMailDetail extends OutgoingMailListItem {
  recipientName: string | null
  body: string | null
  letterTemplateId: number | null
  signatoryPositionId: number | null
  inReplyToIncomingMailId: number | null
  approvedBy: number | null
  approvedAt: string | null
  timeline: TimelineEntry[]
}

export interface CreateOutgoingMail {
  recipientEntity: string
  recipientName?: string | null
  subject: string
  body?: string | null
  letterTemplateId?: number | null
  signatoryPositionId?: number | null
  confidentiality: Confidentiality
  priority: PriorityLevel
  inReplyToIncomingMailId?: number | null
}

// 0 SubmitForApproval, 1 Approve, 2 Send, 3 Archive
export type OutgoingAction = 0 | 1 | 2 | 3

export const outgoingMail = {
  list: (params: { search?: string; status?: number; page?: number; pageSize?: number }) =>
    api.get<PagedResult<OutgoingMailListItem>>('/outgoing-mail', { params }).then((r) => r.data),

  get: (id: number) => api.get<OutgoingMailDetail>(`/outgoing-mail/${id}`).then((r) => r.data),

  create: (body: CreateOutgoingMail) =>
    api.post<OutgoingMailDetail>('/outgoing-mail', body).then((r) => r.data),

  act: (id: number, action: OutgoingAction, note?: string | null) =>
    api.post<OutgoingMailDetail>(`/outgoing-mail/${id}/actions`, { action, note: note ?? null }).then((r) => r.data),
}

export const OUT_STATUS_LABELS: Record<string, string> = {
  Draft: 'مسودة',
  PendingApproval: 'بانتظار الاعتماد',
  Approved: 'معتمدة',
  Sent: 'مُرسلة',
  Archived: 'مؤرشفة',
}

export const OUT_ACTION_LABELS: Record<string, string> = {
  Created: 'إنشاء',
  SubmittedForApproval: 'إرسال للاعتماد',
  Approved: 'اعتماد',
  Sent: 'إرسال',
  Archived: 'أرشفة',
  Edited: 'تعديل',
}
