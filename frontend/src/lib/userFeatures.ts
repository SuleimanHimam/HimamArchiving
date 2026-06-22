import { api } from './api'

export interface Folder { id: number; name: string; parentId: number | null; documentCount: number }
export interface Share { documentId: number; sharedWithUserId: number; userName: string; canEdit: boolean; createdAt: string }

export const foldersApi = {
  list: () => api.get<Folder[]>('/folders').then((r) => r.data),
  create: (b: { name: string; parentId?: number | null }) => api.post<Folder>('/folders', b).then((r) => r.data),
  update: (id: number, b: { name: string; parentId?: number | null }) => api.put<Folder>(`/folders/${id}`, b).then((r) => r.data),
  remove: (id: number) => api.delete(`/folders/${id}`),
  moveDocument: (docId: number, folderId: number | null) => api.put(`/documents/${docId}/folder`, { folderId }),
}

export const favoritesApi = {
  add: (docId: number) => api.post(`/documents/${docId}/favorite`),
  remove: (docId: number) => api.delete(`/documents/${docId}/favorite`),
}

export interface DocNote { id: number; userId: number; authorName: string; content: string; createdAt: string }
export const docNotesApi = {
  list: (docId: number) => api.get<DocNote[]>(`/documents/${docId}/notes`).then((r) => r.data),
  add: (docId: number, content: string) => api.post<DocNote>(`/documents/${docId}/notes`, { content }).then((r) => r.data),
  remove: (docId: number, noteId: number) => api.delete(`/documents/${docId}/notes/${noteId}`),
}

export const sharingApi = {
  list: (docId: number) => api.get<Share[]>(`/documents/${docId}/shares`).then((r) => r.data),
  share: (docId: number, userId: number, canEdit: boolean) => api.post(`/documents/${docId}/shares`, { userId, canEdit }),
  unshare: (docId: number, userId: number) => api.delete(`/documents/${docId}/shares/${userId}`),
}

async function downloadBlob(url: string, filename: string) {
  const res = await api.get(url, { responseType: 'blob' })
  const blobUrl = URL.createObjectURL(res.data as Blob)
  const a = document.createElement('a')
  a.href = blobUrl; a.download = filename; a.click()
  setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000)
}

export const exportApi = {
  documentZip: (docId: number, name: string) => downloadBlob(`/documents/${docId}/zip`, `${name}.zip`),
  exportAll: (params?: { favoritesOnly?: boolean; folderId?: number }) => {
    const q = new URLSearchParams()
    if (params?.favoritesOnly) q.set('favoritesOnly', 'true')
    if (params?.folderId) q.set('folderId', String(params.folderId))
    return downloadBlob(`/documents/export?${q.toString()}`, 'documents-export.zip')
  },
}
