import { api } from './api'
import type { PagedResult } from './shared'

// ── الديوان — external correspondence register ──
export const DiwanDirection = { Incoming: 0, Outgoing: 1 } as const

export interface DiwanAttachment {
  id: number; storageKey: string; fileName: string; contentType: string | null
  sizeBytes: number; version: number; ocrStatus: string
}

export interface DiwanListItem {
  id: number
  registerNumber: string
  referenceNumber: string | null
  direction: string                 // Incoming | Outgoing
  subject: string
  externalOrgName: string | null
  /** Who the letter is addressed to — named on صادر, where the body alone does not say. */
  addresseeName: string | null
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
  isArchived: boolean
  hasAttachments: boolean
}

export interface DiwanDetail {
  id: number; registerNumber: string; referenceNumber: string | null; direction: string
  externalOrgId: number | null; externalOrgName: string | null
  addresseeName: string | null; addresseeTitle: string | null
  subject: string; description: string | null; body: string | null; keywords: string | null
  classification: string; priority: string; status: string
  actionType: string | null; responsePeriodDays: number | null; remindOnNoReply: boolean
  fileNumber: string | null; isArchived: boolean; archivedDocumentId: number | null
  mailDate: string; registrationDate: string
  issueDate: string | null; sentDate: string | null
  dueDate: string | null; responseDeadline: string | null
  categoryId: number | null; categoryName: string | null
  referenceToId: number | null; referenceToNumber: string | null
  toDiwanOrgUnits: OrgUnitRef[]
  attachments: DiwanAttachment[]
  /** Who wrote/registered the letter and their department — printing resolves the letterhead from
   *  these, so a letter keeps its author's design whoever later sends it to the printer. */
  createdByUserId: number | null
  createdByOrgUnitId: number | null
}

/** Which register a mail type is offered on. */
export type MailScopeName = 'External' | 'Internal' | 'Both'
export const MAIL_SCOPE_VALUE: Record<MailScopeName, number> = { External: 0, Internal: 1, Both: 2 }
export const MAIL_SCOPE_AR: Record<MailScopeName, string> = {
  External: 'خارجي (الديوان)', Internal: 'داخلي (المراسلات)', Both: 'كلاهما',
}
/** Query value for the register being composed on. */
export const MAIL_SCOPE_EXTERNAL = 0
export const MAIL_SCOPE_INTERNAL = 1

export interface MailCategory {
  id: number
  name: string
  nameEn: string | null
  sortOrder: number
  scope: MailScopeName
  isActive: boolean
}

export interface SaveMailCategory {
  name: string
  nameEn?: string | null
  scope: number
  sortOrder: number
  isActive?: boolean
}

/** A routing target (الى ديوان). */
export interface OrgUnitRef { id: number; name: string }

/** One person who can open an external letter. `reason` is `Granted` — named by the head, and
 *  revocable — or `Registrar`/`Head`, which come with the letter and cannot be revoked here. */
export interface DiwanAccessEntry {
  userId: number
  fullName: string
  orgUnitName: string | null
  reason: 'Registrar' | 'Head' | 'Granted'
  note: string | null
}

export interface DiwanAccessCandidate { userId: number; fullName: string; orgUnitName: string | null }

/** من يطّلع على البريد — the whole access panel for one letter, in one round trip. */
export interface DiwanAccessView {
  canManage: boolean
  entries: DiwanAccessEntry[]
  candidates: DiwanAccessCandidate[]
}

/** حالة البريد — mirrors CorrespondenceStatus; index is the enum value. */
export const STATUS_NAMES = [
  'Draft', 'Registered', 'UnderReview', 'Approved', 'Sent',
  'Replied', 'OnHold', 'Overdue', 'Closed', 'Archived',
] as const

export interface SaveDiwanRequest {
  direction: number
  subject: string
  referenceNumber: string | null
  externalOrgId: number | null
  externalOrgName: string | null
  addresseeName: string | null
  addresseeTitle: string | null
  classification: number
  priority: number
  mailDate: string
  issueDate: string | null            // تاريخ إصدار البريد
  sentDate: string | null             // تاريخ الإرسال
  dueDate: string | null
  responseDeadline: string | null     // تاريخ الرد على البريد
  responsePeriodDays: number | null
  remindOnNoReply: boolean
  actionType: number | null
  categoryId: number | null           // تصنيف البريد
  referenceToId: number | null        // مرجع الى
  toDiwanOrgUnitIds: number[]         // الى ديوان — one or more bureaus
  description: string | null
  body: string | null
  keywords: string | null
  fileNumber: string | null           // اسم الملف
}

export interface DiwanFilter {
  folder?: string                    // all | incoming | outgoing | drafts | deleted
  direction?: number; status?: number; classification?: number
  from?: string; to?: string; page?: number; pageSize?: number
}

/** One letter in a register chain — the original, its replies, and entries citing it as مرجع. */
export interface DiwanThreadNode {
  id: number
  registerNumber: string
  referenceNumber: string | null
  subject: string
  direction: string                  // Incoming | Outgoing
  status: string
  relation: string                   // Root | Reply | Reference
  mailDate: string
  externalOrgName: string | null
  body: string | null
  hasAttachments: boolean
}

export interface ReplyDiwanRequest {
  subject: string
  body: string | null
  classification: number
  priority: number
  mailDate: string | null
  dueDate: string | null
  responseDeadline: string | null
  toDiwanOrgUnitIds: number[]
}

/** Convert a register entry into an internal message addressed to staff/departments/groups. */
export interface ConvertToInternalRequest {
  subject: string | null
  body: string | null
  notes: string | null
  classification: number | null
  priority: number | null
  actionType: number | null
  dueDate: string | null
  responseDeadline: string | null
  copyAttachments: boolean
  recipients: { addressing: number; targetKind: number; targetId: number | null; targetName: string | null }[]
}

export const diwan = {
  list: (f: DiwanFilter = {}) => api.get<PagedResult<DiwanListItem>>('/diwan', { params: f }).then((r) => r.data),
  thread: (id: number) => api.get<DiwanThreadNode[]>(`/diwan/${id}/thread`).then((r) => r.data),
  reply: (id: number, b: ReplyDiwanRequest) =>
    api.post<DiwanDetail>(`/diwan/${id}/reply`, b).then((r) => r.data),
  convertToInternal: (id: number, b: ConvertToInternalRequest) =>
    api.post<{ internalMailId: number }>(`/diwan/${id}/to-internal`, b).then((r) => r.data),
  get: (id: number) => api.get<DiwanDetail>(`/diwan/${id}`).then((r) => r.data),
  search: (q: string, page = 1, pageSize = 25) =>
    api.get<PagedResult<DiwanListItem>>('/diwan/search', { params: { q, page, pageSize } }).then((r) => r.data),
  create: (b: SaveDiwanRequest) => api.post<DiwanDetail>('/diwan', b).then((r) => r.data),
  /** Mail types. `scope` limits them to one register (types marked Both always appear);
   *  omit it for the whole catalogue, which is what settings and the print designer show. */
  categories: (opts?: { scope?: number; includeInactive?: boolean }) =>
    api.get<MailCategory[]>('/diwan/categories', {
      params: { scope: opts?.scope, includeInactive: opts?.includeInactive || undefined },
    }).then((r) => r.data),
  createCategory: (b: SaveMailCategory) =>
    api.post<MailCategory>('/diwan/categories', b).then((r) => r.data),
  updateCategory: (id: number, b: SaveMailCategory) =>
    api.put<MailCategory>(`/diwan/categories/${id}`, b).then((r) => r.data),
  removeCategory: (id: number) => api.delete(`/diwan/categories/${id}`),
  changeStatus: (id: number, b: { status: number; note: string | null; effectiveDate: string | null }) =>
    api.post(`/diwan/${id}/status`, b).then((r) => r.data),
  /** من يطّلع على البريد — who can open this letter, and whether the caller may change it. */
  access: (id: number) => api.get<DiwanAccessView>(`/diwan/${id}/access`).then((r) => r.data),
  /** Replaces the grant list: whoever is not named loses access. */
  setAccess: (id: number, userIds: number[], note?: string | null) =>
    api.put(`/diwan/${id}/access`, { userIds, note: note ?? null }).then((r) => r.data),
  archive: (id: number) => api.post(`/diwan/${id}/archive`).then((r) => r.data),
  remove: (id: number) => api.delete(`/diwan/${id}`).then((r) => r.data),
  restore: (id: number) => api.post(`/diwan/${id}/restore`).then((r) => r.data),
  addAttachment: (id: number, file: File) => {
    const fd = new FormData()
    fd.append('file', file)
    return api.post<DiwanAttachment>(`/diwan/${id}/attachments`, fd).then((r) => r.data)
  },

  /** All attachments merged into one PDF (printing the scanned original). Caller revokes the URL. */
  fetchCombinedUrl: async (id: number): Promise<string> => {
    const res = await api.get(`/diwan/${id}/attachments/combined`, { responseType: 'blob' })
    return URL.createObjectURL(new Blob([res.data as Blob], { type: 'application/pdf' }))
  },
}
