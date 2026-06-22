import { api } from './api'
import type { PagedResult, Confidentiality } from './incomingMail'

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
}

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

export const documents = {
  list: (params: { search?: string; status?: number; documentTypeId?: number; page?: number; pageSize?: number;
    dateFrom?: string; dateTo?: string; favoritesOnly?: boolean; sharedWithMe?: boolean; folderId?: number;
    customFieldId?: number; customFieldValue?: string }) =>
    api.get<PagedResult<DocumentListItem>>('/documents', { params }).then((r) => r.data),

  get: (id: number) => api.get<DocumentDetail>(`/documents/${id}`).then((r) => r.data),

  create: (body: CreateDocument) => api.post<DocumentDetail>('/documents', body).then((r) => r.data),

  update: (id: number, body: Partial<CreateDocument> & { expiryDate?: string | null }) =>
    api.put<DocumentDetail>(`/documents/${id}`, body).then((r) => r.data),

  remove: (id: number) => api.delete(`/documents/${id}`),

  types: () => api.get<DocumentTypeDto[]>('/documents/types').then((r) => r.data),
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
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
