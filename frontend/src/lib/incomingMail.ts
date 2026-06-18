import { api } from './api'

export type Confidentiality = 0 | 1 | 2 | 3 // Public, Internal, Confidential, HighlyConfidential
export type PriorityLevel = 0 | 1 | 2 | 3 // Low, Normal, High, Urgent

export interface IncomingMailListItem {
  id: number
  transactionNumber: string
  senderEntity: string
  subject: string
  confidentiality: string
  priority: string
  status: string
  receivedDate: string
  assignedToPositionId: number | null
  assignedToOrgUnitId: number | null
  createdAt: string
}

export interface TimelineEntry {
  id: number
  action: string
  userId: number | null
  at: string
  note: string | null
}

export interface IncomingMailDetail extends IncomingMailListItem {
  senderName: string | null
  senderReference: string | null
  body: string | null
  issueDate: string | null
  keywords: string | null
  assignedToUserId: number | null
  parentMailId: number | null
  timeline: TimelineEntry[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface CreateIncomingMail {
  senderEntity: string
  senderName?: string | null
  senderReference?: string | null
  subject: string
  body?: string | null
  issueDate?: string | null
  receivedDate: string
  confidentiality: Confidentiality
  priority: PriorityLevel
  keywords?: string | null
}

// 0 Forward, 1 Approve, 2 Hold, 3 Close, 4 Archive
export type MailAction = 0 | 1 | 2 | 3 | 4

export const incomingMail = {
  list: (params: { search?: string; status?: number; page?: number; pageSize?: number }) =>
    api.get<PagedResult<IncomingMailListItem>>('/incoming-mail', { params }).then((r) => r.data),

  get: (id: number) =>
    api.get<IncomingMailDetail>(`/incoming-mail/${id}`).then((r) => r.data),

  create: (body: CreateIncomingMail) =>
    api.post<IncomingMailDetail>('/incoming-mail', body).then((r) => r.data),

  act: (id: number, action: MailAction, payload: { toUserId?: number | null; note?: string | null } = {}) =>
    api.post<IncomingMailDetail>(`/incoming-mail/${id}/actions`, { action, ...payload }).then((r) => r.data),
}

export const CONFIDENTIALITY_LABELS: Record<string, { ar: string; cls: string }> = {
  Public: { ar: 'عام', cls: 'public' },
  Internal: { ar: 'داخلي', cls: 'internal' },
  Confidential: { ar: 'سري', cls: 'confidential' },
  HighlyConfidential: { ar: 'سري للغاية', cls: 'secret' },
}

export const STATUS_LABELS: Record<string, string> = {
  New: 'جديدة',
  Assigned: 'محالة',
  InProgress: 'قيد المعالجة',
  OnHold: 'معلّقة',
  Closed: 'مغلقة',
  Archived: 'مؤرشفة',
}

export const PRIORITY_LABELS: Record<string, string> = {
  Low: 'منخفضة',
  Normal: 'عادية',
  High: 'عالية',
  Urgent: 'عاجلة',
}
