import { api } from './api'
import type { PagedResult, Confidentiality } from './shared'
import type { LocationKind } from './locations'

export type ScanFormat = 'pdf' | 'jpg' | 'png'

export interface DocumentTypeDto {
  id: number
  name: string
  nameEn: string | null
  code: string | null
  categoryId: number | null
  defaultConfidentiality: string
  retentionMonths: number
  requiresApproval: boolean
  allowedUploadSources: string // flags string, e.g. "Scanner" or "All"
  isActive: boolean
  /** Owning الدائرة; null = عام, offered to every department. */
  orgUnitId: number | null
  orgUnitName: string | null
  number: number | null       // الرقم — the department's own reference number
  note: string | null         // ملاحظة
  retentionValue: number      // مدة الحفظ as entered
  retentionUnit: RetentionUnitName
}

/** Unit a retention period is expressed in. retentionMonths stays as the derived value. */
export type RetentionUnitName = 'Days' | 'Months' | 'Years'
export const RETENTION_UNIT_VALUE: Record<RetentionUnitName, number> = { Days: 0, Months: 1, Years: 2 }
export const RETENTION_UNIT_AR: Record<RetentionUnitName, string> = { Days: 'يوم', Months: 'شهر', Years: 'سنة' }
export const RETENTION_UNITS: { v: number; name: RetentionUnitName; ar: string }[] = [
  { v: 0, name: 'Days', ar: 'يوم' },
  { v: 1, name: 'Months', ar: 'شهر' },
  { v: 2, name: 'Years', ar: 'سنة' },
]

export interface DocumentCategoryDto {
  id: number
  parentId: number | null
  name: string
  code: string | null
  isActive: boolean
}

// UploadSource flags: Scanner=1, Pdf=2, Image=4, AnyFile=8, All=15
export interface DocumentTypeInput {
  name: string
  nameEn?: string | null
  code?: string | null
  categoryId?: number | null
  defaultConfidentiality: number
  retentionMonths: number
  requiresApproval: boolean
  allowedUploadSources: number
  isActive?: boolean
  orgUnitId?: number | null   // null = عام
  number?: number | null
  note?: string | null
  retentionValue?: number     // مدة الحفظ as entered; retentionMonths is derived server-side
  retentionUnit?: number      // 0 Days · 1 Months · 2 Years
}

export interface DocumentListItem {
  id: number
  documentNumber: string
  title: string
  documentTypeName: string
  confidentiality: string
  status: string
  version: number
  documentDate: string | null
  expiryDate: string | null
  createdAt: string
  physicalLocationName: string | null
  boxNumber: string | null
  fileNumber: string | null
  boxCode: string | null
  fileExtension: string | null            // primary attachment's extension (pdf|docx|…); null when none
  attachmentCount: number
  totalSizeBytes: number                  // combined size of the attached files
  updatedAt: string | null
  primaryAttachmentId: number | null      // that same attachment, for fetching a real thumbnail
  customValues?: Record<string, string>   // admin custom field values, keyed by field id
}

export interface DocumentAttachmentDto {
  id: number
  fileName: string
  contentType: string
  fileExtension: string
  sizeBytes: number
  isScanned: boolean
  createdAt: string
}

export interface TrashItem {
  id: number
  documentNumber: string
  title: string
  documentTypeName: string
  fileExtension: string | null
  attachmentCount: number
  totalSizeBytes: number
  deletedAt: string | null
  deletedByName: string | null
}

export interface DocumentAttachmentInfo {
  id: number
  fileName: string
  fileExtension: string
  sizeBytes: number
  isScanned: boolean
  createdAt: string
  uploadedByName: string | null
}

/** What the «خصائص» dialog shows: the metadata card plus every attached file. */
export interface DocumentProperties {
  id: number
  documentNumber: string
  title: string
  documentTypeName: string
  confidentiality: string
  status: string
  version: number
  documentDate: string | null
  createdAt: string
  createdByName: string | null
  updatedAt: string | null
  updatedByName: string | null
  folderName: string | null
  /** Palette key and symbol of that folder, so the card draws it as the browser does. */
  folderColor: string | null
  folderIcon: string | null
  physicalLocationName: string | null
  attachmentCount: number
  totalSizeBytes: number
  attachments: DocumentAttachmentInfo[]
}

export interface DocumentDetail extends DocumentListItem {
  description: string | null
  documentTypeId: number
  categoryId: number | null
  owningOrgUnitId: number
  ownerPositionId: number | null
  keywords: string | null
  retentionMonths: number
  parentDocumentId: number | null
  isLatestVersion: boolean
  attachments: DocumentAttachmentDto[]
  physicalLocationId: number | null
  folderId: number | null
  isFavorite: boolean
  isTombstone?: boolean
  destroyedAtUtc?: string | null
  boxId?: number | null
  // Archiving path
  archiveMode?: string        // 'Manual' | 'Correspondence'
  linkedMailId?: number | null
  linkedMailDirection?: string | null   // 'Incoming' | 'Outgoing'
  /** Why the archive department sent it back, if it did — shown so the objection can be acted on. */
  archiveReturnReason?: string | null
  /** When it was handed to the archive; null once filed or returned. */
  sentToArchiveAtUtc?: string | null
  // physicalLocationName, boxNumber, fileNumber, boxCode inherited from DocumentListItem
}

export interface CreateDocument {
  title: string
  description?: string | null
  documentTypeId: number
  categoryId?: number | null
  owningOrgUnitId: number
  ownerPositionId?: number | null
  confidentiality: Confidentiality
  keywords?: string | null
  documentDate?: string | null
  physicalLocationId?: number | null
  boxNumber?: string | null
  fileNumber?: string | null
  boxId?: number | null
  // Archiving path: 0 Manual (standalone) | 1 Correspondence (linked to a mail record)
  archiveMode?: 0 | 1
  linkedMailId?: number | null
  linkedMailDirection?: 0 | 1 | null   // 0 Incoming | 1 Outgoing
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

/** A document sitting in the archive department's intake queue. */
export interface ArchiveIntakeItem {
  id: number
  documentNumber: string
  title: string
  documentTypeName: string
  owningOrgUnitId: number
  owningOrgUnitName: string
  confidentiality: string
  documentDate: string | null
  /** When it was handed over — the queue is worked oldest-first. */
  sentAt: string | null
  sentByName: string | null
  attachmentCount: number
  /** Set when this is a second attempt: why it was sent back last time. */
  previousReturnReason: string | null
}

export interface DocumentVerification {
  documentId: number
  documentNumber: string
  /** The 10-character code printed under the QR, for anyone typing it by hand. */
  code: string
  /** Where a scan lands, relative to the site root. */
  verifyPath: string
  /** The QR image, relative to the site root. */
  qrPath: string
}

export const documents = {
  list: (params: { search?: string; status?: number; documentTypeId?: number; page?: number; pageSize?: number;
    dateFrom?: string; dateTo?: string; favoritesOnly?: boolean; sharedWithMe?: boolean; folderId?: number;
    /** Only documents filed into no folder — the file browser's root listing. */
    unfiledOnly?: boolean;
    customFieldId?: number; customFieldValue?: string; boxId?: number;
    /** A place at any level — everything filed underneath it is returned. */
    locationKind?: LocationKind; locationId?: number }) =>
    api.get<PagedResult<DocumentListItem>>('/documents', { params }).then((r) => r.data),

  get: (id: number) => api.get<DocumentDetail>(`/documents/${id}`).then((r) => r.data),
  /** The document's verification code and the paths behind its printed QR. */
  verification: (id: number) =>
    api.get<DocumentVerification>(`/documents/${id}/verification`).then((r) => r.data),
  properties: (id: number) =>
    api.get<DocumentProperties>(`/documents/${id}/properties`).then((r) => r.data),

  create: (body: CreateDocument) => api.post<DocumentDetail>('/documents', body).then((r) => r.data),

  update: (id: number, body: Partial<CreateDocument> & { expiryDate?: string | null }) =>
    api.put<DocumentDetail>(`/documents/${id}`, body).then((r) => r.data),

  remove: (id: number) => api.delete(`/documents/${id}`),

  // ---- Trash ----
  trash: () => api.get<TrashItem[]>('/documents/trash').then((r) => r.data),
  restore: (id: number) => api.post(`/documents/${id}/restore`),
  /** Permanent — the content is crypto-shredded and cannot be brought back. */
  purge: (id: number) => api.delete(`/documents/${id}/purge`),
  emptyTrash: () => api.delete<{ destroyed: number }>('/documents/trash').then((r) => r.data),
  sendToArchive: (id: number) => api.post(`/documents/${id}/send-to-archive`, {}).then((r) => r.data),

  // ── The archive department's intake queue ──
  /** Documents waiting for a physical place, oldest first. Needs Archive.Archive. */
  archiveQueue: () => api.get<ArchiveIntakeItem[]>('/documents/archive-queue').then((r) => r.data),
  /** File a waiting document into a box — this is what marks it مؤرشف. */
  fileToArchive: (id: number, boxId: number, note?: string | null) =>
    api.post(`/documents/${id}/file-to-archive`, { boxId, note: note ?? null }).then((r) => r.data),
  /** Send it back to whoever submitted it; the reason reaches them as a notification. */
  returnFromArchive: (id: number, reason: string) =>
    api.post(`/documents/${id}/return-from-archive`, { reason }).then((r) => r.data),

  /** Without orgUnitId the whole catalogue; with it, that department's types plus the shared (عام) ones. */
  types: (orgUnitId?: number) =>
    api.get<DocumentTypeDto[]>('/documents/types', { params: { orgUnitId } }).then((r) => r.data),
  createType: (body: DocumentTypeInput) => api.post<DocumentTypeDto>('/documents/types', body).then((r) => r.data),
  updateType: (id: number, body: DocumentTypeInput) => api.put<DocumentTypeDto>(`/documents/types/${id}`, body).then((r) => r.data),
  deleteType: (id: number) => api.delete(`/documents/types/${id}`),
  categories: () => api.get<DocumentCategoryDto[]>('/documents/categories').then((r) => r.data),
  createCategory: (b: { name: string; code?: string | null; parentId?: number | null }) =>
    api.post<DocumentCategoryDto>('/documents/categories', b).then((r) => r.data),
  updateCategory: (id: number, b: { name: string; code?: string | null; parentId?: number | null; isActive: boolean }) =>
    api.put<DocumentCategoryDto>(`/documents/categories/${id}`, b).then((r) => r.data),
  deleteCategory: (id: number) => api.delete(`/documents/categories/${id}`),

  orgUnits: () => api.get<OrgUnitDto[]>('/organization/org-units').then((r) => r.data),
  users: () => api.get<{ id: number; fullName: string; email: string }[]>('/organization/users').then((r) => r.data),

  upload: (id: number, file: File) => {
    const fd = new FormData()
    fd.append('file', file)
    return api
      .post<DocumentAttachmentDto>(`/documents/${id}/attachments`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  removeAttachment: (id: number, attachmentId: number) =>
    api.delete(`/documents/${id}/attachments/${attachmentId}`),

  // Upload an image that came from a scanner; the server outputs the chosen format (pdf | jpg | png)
  // and flags the attachment as scanned.
  scan: (id: number, file: Blob, fileName: string, format: ScanFormat = 'pdf') => {
    const fd = new FormData()
    fd.append('file', file, fileName)
    return api
      .post<DocumentAttachmentDto>(`/documents/${id}/scan`, fd, {
        params: { format },
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  // Download an attachment honoring the bearer token, then trigger a save.
  download: async (id: number, attachmentId: number, fileName: string) => {
    const res = await api.get(`/documents/${id}/attachments/${attachmentId}`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = fileName
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },

  // Fetch an attachment (with auth) as an object URL for in-page preview. Caller revokes it.
  fetchObjectUrl: async (id: number, attachmentId: number, contentType: string): Promise<string> => {
    const res = await api.get(`/documents/${id}/attachments/${attachmentId}`, { responseType: 'blob' })
    const blob = new Blob([res.data as Blob], { type: contentType || 'application/octet-stream' })
    return URL.createObjectURL(blob)
  },

  // All attachments merged into one PDF (for "print all attachments"). Caller revokes the URL.
  fetchCombinedUrl: async (id: number): Promise<string> => {
    const res = await api.get(`/documents/${id}/attachments/combined`, { responseType: 'blob' })
    const blob = new Blob([res.data as Blob], { type: 'application/pdf' })
    return URL.createObjectURL(blob)
  },
}

export const DOC_STATUS_LABELS: Record<string, string> = {
  Draft: 'مسودة',
  Active: 'نشطة',
  Archived: 'مؤرشفة',
  PendingDisposal: 'بانتظار الإتلاف',
  Disposed: 'مُتلَفة',
  PendingArchive: 'بانتظار الأرشفة',
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
