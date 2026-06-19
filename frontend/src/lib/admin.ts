import { api } from './api'

export interface BackupStatus {
  mysqldumpFound: boolean
  mysqlFound: boolean
  mysqldumpPath: string | null
  mysqlPath: string | null
}

export interface AutoBackupSettings {
  enabled: boolean
  targetPath: string | null
  intervalHours: number
  lastRunAt: string | null
  lastRunStatus: 'Success' | 'Failed' | null
  lastRunError: string | null
}

export interface BrowseDirEntry {
  name: string
  fullPath: string
}

export interface BrowseResult {
  currentPath: string | null
  parentPath: string | null
  directories: BrowseDirEntry[]
  error: string | null
}

export const admin = {
  backupStatus: () =>
    api.get<BackupStatus>('/admin/backup/status').then((r) => r.data),

  // Download backup as a blob and trigger browser save-dialog.
  downloadBackup: async (): Promise<void> => {
    const resp = await api.get('/admin/backup', { responseType: 'blob' })
    const cd = resp.headers['content-disposition'] ?? ''
    const match = cd.match(/filename="?([^"]+)"?/)
    const name = match?.[1] ?? `archiving_backup_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.sql`

    const url = URL.createObjectURL(resp.data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    a.click()
    URL.revokeObjectURL(url)
  },

  restore: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.post<{ restored: boolean; file: string; bytes: number }>(
      '/admin/restore', form,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    ).then((r) => r.data)
  },

  autoBackup: {
    get: () => api.get<AutoBackupSettings>('/admin/backup/auto').then((r) => r.data),

    update: (body: { enabled: boolean; targetPath: string | null; intervalHours: number }) =>
      api.put<AutoBackupSettings>('/admin/backup/auto', body).then((r) => r.data),

    testPath: (path: string) =>
      api.post<{ ok: boolean; error: string | null }>('/admin/backup/auto/test-path', { path }).then((r) => r.data),

    browse: (path?: string) =>
      api.get<BrowseResult>('/admin/backup/auto/browse', { params: path ? { path } : {} }).then((r) => r.data),
  },
}
