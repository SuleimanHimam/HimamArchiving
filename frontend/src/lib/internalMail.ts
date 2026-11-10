import { api } from './api'
import type { PagedResult } from './shared'
import type { WorkflowMailBadge } from './workflow'

// ── المراسلات — internal correspondence ──
export const Addressing = { To: 0, Cc: 1, Bcc: 2 } as const
// Internal targets only (mirrors RecipientKind; External is intentionally excluded).
export const TargetKind = { Employee: 1, Department: 2, Group: 4 } as const

export interface InternalRecipient {
  id: number; addressing: string; targetKind: string; targetId: number | null; targetName: string | null
}
export interface InternalAttachment {
  id: number; storageKey: string; fileName: string; contentType: string | null
  sizeBytes: number; version: number; ocrStatus: string
}
export interface InternalReadRow {
  userId: number; userName: string | null; readStatus: string
  deliveredAt: string | null; firstReadAt: string | null; lastReadAt: string | null
}
export interface InternalThreadNode {
  id: number; mailNumber: string; subject: string; status: string; linkType: string
  mailDate: string; senderName: string | null; body: string | null
}

export interface InternalListItem {
  id: number
  mailNumber: string
  referenceNumber: string | null
  subject: string
  senderName: string | null
  classification: string
  priority: string
  status: string
  actionType: string | null
  categoryId: number | null
  categoryName: string | null
  mailDate: string
  registrationDate: string
  dueDate: string | null
  responseDeadline: string | null
  isRead: boolean
  hasAttachments: boolean
  /** صادر as *this* reader sees it — true when they sent it. The same message is outgoing to its
   *  author and incoming to everyone it reached, so the server computes it per query. */
  isOutgoing: boolean
  /** The To line. The sent folder lists by recipient — "from" there is always the reader. */
  recipientNames: string[] | null
  /** Set when the row is part of a workflow run: the route, the step, and whether it waits on you.
   *  The inbox is the workflow inbox — there is no second list. */
  workflow: WorkflowMailBadge | null
}

export interface InternalDetail {
  id: number; mailNumber: string; referenceNumber: string | null
  senderUserId: number; senderName: string | null
  subject: string; description: string | null; body: string | null; keywords: string | null
  classification: string; priority: string; status: string
  actionType: string | null; responsePeriodDays: number | null; remindOnNoReply: boolean
  /** تصنيف البريد — قسيمة راتب / سند قبض / سند دفع … Also picks the letterhead it prints on. */
  categoryId: number | null; categoryName: string | null
  fileNumber: string | null; isArchived: boolean; archivedDocumentId: number | null
  mailDate: string; registrationDate: string; dueDate: string | null; responseDeadline: string | null
  replyToId: number | null; parentId: number | null
  recipients: InternalRecipient[]; attachments: InternalAttachment[]
}

export interface RecipientInput {
  addressing: number; targetKind: number; targetId: number | null; targetName: string | null
}

export interface ComposeInternalRequest {
  subject: string
  referenceNumber: string | null
  classification: number
  priority: number
  dueDate: string | null
  responseDeadline: string | null
  responsePeriodDays: number | null
  remindOnNoReply: boolean
  actionType: number | null
  categoryId: number | null
  description: string | null
  body: string | null
  keywords: string | null
  fileNumber: string | null
  recipients: RecipientInput[]
}

export interface InternalFilter {
  folder?: string                    // all | inbox | sent | drafts | deleted
  status?: number; classification?: number
  from?: string; to?: string; page?: number; pageSize?: number
}

export const internalMail = {
  list: (f: InternalFilter = {}) => api.get<PagedResult<InternalListItem>>('/internal-mail', { params: f }).then((r) => r.data),
  get: (id: number) => api.get<InternalDetail>(`/internal-mail/${id}`).then((r) => r.data),
  search: (q: string, page = 1, pageSize = 25) =>
    api.get<PagedResult<InternalListItem>>('/internal-mail/search', { params: { q, page, pageSize } }).then((r) => r.data),
  thread: (id: number) => api.get<InternalThreadNode[]>(`/internal-mail/${id}/thread`).then((r) => r.data),
  readStatus: (id: number) => api.get<InternalReadRow[]>(`/internal-mail/${id}/read-status`).then((r) => r.data),

  compose: (b: ComposeInternalRequest) => api.post<InternalDetail>('/internal-mail', b).then((r) => r.data),
  reply: (id: number, b: { subject: string; body: string | null; classification: number; priority: number; replyAll: boolean; additionalRecipients: RecipientInput[] | null }) =>
    api.post<InternalDetail>(`/internal-mail/${id}/reply`, b).then((r) => r.data),
  forward: (id: number, b: { body: string | null; recipients: RecipientInput[] }) =>
    api.post<InternalDetail>(`/internal-mail/${id}/forward`, b).then((r) => r.data),

  markRead: (id: number) => api.post(`/internal-mail/${id}/read`).then((r) => r.data),
  archive: (id: number) => api.post(`/internal-mail/${id}/archive`).then((r) => r.data),
  remove: (id: number) => api.delete(`/internal-mail/${id}`).then((r) => r.data),
  restore: (id: number) => api.post(`/internal-mail/${id}/restore`).then((r) => r.data),
  addAttachment: (id: number, file: File) => {
    const fd = new FormData()
    fd.append('file', file)
    return api.post<InternalAttachment>(`/internal-mail/${id}/attachments`, fd).then((r) => r.data)
  },

  /** All attachments merged into one PDF (printing the scanned original). Caller revokes the URL. */
  fetchCombinedUrl: async (id: number): Promise<string> => {
    const res = await api.get(`/internal-mail/${id}/attachments/combined`, { responseType: 'blob' })
    return URL.createObjectURL(new Blob([res.data as Blob], { type: 'application/pdf' }))
  },
}

// Classification badge classes shared by both mail modules.
export const CLASS_CLS: Record<string, string> = {
  Public: 'public', Internal: 'internal', Confidential: 'confidential', HighlyConfidential: 'secret',
}
// Detail APIs return the classification name; save APIs take the enum value.
export const CLASS_VALUES: Record<string, number> = {
  Public: 0, Internal: 1, Confidential: 2, HighlyConfidential: 3,
}
// The other direction, for forms that hold the enum value while the rest of the app speaks names.
export const CLASS_KEYS: Record<number, string> = Object.fromEntries(
  Object.entries(CLASS_VALUES).map(([k, v]) => [v, k]),
)
// Directed-action names (mirror CorrespondenceActionType).
export const ACTION_NAMES = ['ForAction', 'ForSigning', 'ForReview', 'ForInfo', 'ForReply', 'ForApproval', 'ForArchive'] as const
