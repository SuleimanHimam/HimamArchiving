import { api } from './api'
import type { ItemShare } from './userFeatures'

/**
 * A file kept on its own — no document number, no type, no retention schedule.
 *
 * The register's documents and these files live side by side in the browser, so
 * anything that renders both keeps the two apart by origin, not by guesswork.
 */
export interface UserFile {
  id: number
  name: string
  folderId: number | null
  fileExtension: string
  contentType: string
  sizeBytes: number
  createdAt: string
  updatedAt: string | null
  contentDestroyed: boolean
  /** False on your own files; true on one shared with you, directly or through its folder. */
  shared: boolean
  /** Whether you may rename or move it. Deleting stays with its owner. */
  canEdit: boolean
  /** Whose file it is, on a borrowed one. */
  ownerName: string | null
}

/** The properties card. Shorter than a document's on purpose — see the dialog. */
export interface UserFileProperties {
  id: number
  name: string
  folderName: string | null
  /** Palette key and symbol of that folder, so the card draws it as the browser does. */
  folderColor: string | null
  folderIcon: string | null
  fileExtension: string
  contentType: string
  sizeBytes: number
  checksum: string | null
  createdAt: string
  createdByName: string | null
  updatedAt: string | null
  updatedByName: string | null
  contentDestroyed: boolean
}

/** Grants on one file. Only its owner may read or change this list. */
export const fileSharesApi = {
  list: (id: number) => api.get<ItemShare[]>(`/files/${id}/shares`).then((r) => r.data),
  share: (id: number, userId: number, canEdit: boolean) =>
    api.post(`/files/${id}/shares`, { userId, canEdit }),
  unshare: (id: number, userId: number) => api.delete(`/files/${id}/shares/${userId}`),
  /** One call rather than one per person — the institution can be large. */
  shareEveryone: (id: number, canEdit: boolean) =>
    api.post(`/files/${id}/shares/everyone`, { canEdit }),
  unshareEveryone: (id: number) => api.delete(`/files/${id}/shares/everyone`),
}

export const filesApi = {
  list: (folderId: number | null) =>
    api.get<UserFile[]>('/files', { params: { folderId: folderId ?? undefined } }).then((r) => r.data),

  upload: (file: File, folderId: number | null) => {
    const fd = new FormData()
    fd.append('file', file)
    return api
      .post<UserFile>('/files', fd, {
        params: { folderId: folderId ?? undefined },
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  /** Rename, move, or both — the server treats them as one edit. */
  update: (id: number, body: { name: string; folderId: number | null }) =>
    api.put(`/files/${id}`, body),

  remove: (id: number) => api.delete(`/files/${id}`),

  properties: (id: number) =>
    api.get<UserFileProperties>(`/files/${id}/properties`).then((r) => r.data),

  trash: () => api.get<UserFile[]>('/files/trash').then((r) => r.data),
  restore: (id: number) => api.post(`/files/${id}/restore`),
  purge: (id: number) => api.delete(`/files/${id}/purge`),

  /** Fetched with the bearer token, so a plain <a href> or <img src> will not do. */
  objectUrl: async (id: number, contentType: string) => {
    const res = await api.get(`/files/${id}/content`, { responseType: 'blob' })
    return URL.createObjectURL(new Blob([res.data as Blob], { type: contentType || 'application/octet-stream' }))
  },

  async download(id: number, name: string, contentType: string) {
    const url = await this.objectUrl(id, contentType)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
}
