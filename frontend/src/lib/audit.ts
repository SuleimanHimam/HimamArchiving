import { api } from './api'

export interface AuditLogEntry {
  id: number
  userId: number | null
  userEmail: string | null
  userFullName: string | null
  action: string
  entityType: string | null
  entityId: number | null
  entityTitle: string | null
  ipAddress: string | null
  machineName: string | null
  oldValues: string | null
  newValues: string | null
  createdAt: string // ISO UTC
}

export interface AuditLogsPage {
  total: number
  page: number
  pageSize: number
  items: AuditLogEntry[]
}

export interface AuditLogFilter {
  entityType?: string
  action?: string
  userId?: number | ''
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export interface AuditUser {
  id: number
  fullName: string
  email: string
}

/** Mirrors AuditChainResult on the server. The field names matter: this used to be typed as
 *  `{ valid, checkedCount }`, which the API never sends, so `chain.valid` was always undefined
 *  and the integrity badge read "chain broken" on a perfectly intact log. */
export interface ChainVerifyResult {
  intact: boolean
  total: number
  verified: number
  firstBrokenId?: number | null
  detail?: string | null
}

export interface ResealResult {
  resealed?: number
  [k: string]: unknown
}

export const auditApi = {
  logs: (f: AuditLogFilter = {}) => {
    const params: Record<string, unknown> = { ...f }
    if (!params.userId) delete params.userId
    return api.get<AuditLogsPage>('/audit/logs', { params }).then((r) => r.data)
  },

  entityTypes: () =>
    api.get<string[]>('/audit/entity-types').then((r) => r.data),

  users: () =>
    api.get<AuditUser[]>('/audit/users').then((r) => r.data),

  // Distinct actions actually present in the log. See AuditController.Actions for why this
  // cannot be a fixed client-side list.
  actions: () =>
    api.get<string[]>('/audit/actions').then((r) => r.data),

  verify: () =>
    api.get<ChainVerifyResult>('/audit/verify').then((r) => r.data),

  // Establishes a new verified baseline after a reviewed integrity check. Does not
  // repair or erase history (§8.3) — it anchors verification of subsequent entries.
  // The reseal is itself audited.
  reseal: () =>
    api.post<ResealResult>('/audit/reseal').then((r) => r.data),

  // Soft-hides an entry from the monitoring view (the hash chain stays intact server-side).
  deleteLog: (id: number) => api.delete(`/audit/logs/${id}`),

  // Soft-hides ALL currently-visible entries in one action.
  deleteAllLogs: () => api.delete<{ hidden: number }>('/audit/logs').then((r) => r.data),
}

// Arabic labels for known action codes
export const ACTION_AR: Record<string, string> = {
  View:    'عرض',
  Create:  'إنشاء',
  Edit:    'تعديل',
  Delete:  'حذف',
  Download: 'تنزيل ملف',
  Print:   'طباعة',
  Forward: 'إحالة',
  Approve: 'اعتماد',
  Archive: 'أرشفة',
  Login:   'تسجيل دخول',
  Logout:  'تسجيل خروج',
}

export const ENTITY_AR: Record<string, string> = {
  Documents:    'الوثائق',
  IncomingMail: 'الوارد',
  OutgoingMail: 'الصادر',
  Workflow:     'سير العمل',
  Archive:      'الأرشيف',
  Reports:      'التقارير',
  Users:        'المستخدمون',
  Organization: 'الهيكل التنظيمي',
  Settings:     'الإعدادات',
  Audit:        'سجل المراقبة',
  AuditLog:     'سجل الحركات',
}

/* Both tenses are real: AuditActionFilter derives Create/Edit/Delete/View from the HTTP verb,
   while the services write past-tense semantic actions. Colour is keyed on meaning, so the two
   spellings of one action share a colour. */
export const ACTION_COLOR: Record<string, string> = {
  Create:  'var(--brass)',   Created:  'var(--brass)',
  Edit:    '#1e3a8a',        Edited:   '#1e3a8a',   Updated: '#1e3a8a',
  Delete:  'var(--seal)',    Deleted:  'var(--seal)', Removed: 'var(--seal)',
  View:    '#525252',        Viewed:   '#525252',
  Forward: '#0f766e',        Forwarded: '#0f766e',
  Approve: '#2f5d3a',        Approved: '#2f5d3a',
  Reject:  '#9a3412',        Rejected: '#9a3412',
  Archive: '#374151',        Archived: '#374151',
  Restore: '#0369a1',        Restored: '#0369a1',
  Print:   '#7c3aed',        Printed:  '#7c3aed',
  Download: '#0e7490',       Downloaded: '#0e7490', DownloadAttachment: '#0e7490',
  Login:   '#6b21a8',        Logout:   '#6b21a8',
}

/** Colour for any action code, including the many rare ones no map will ever list. */
export const actionColor = (action: string) => ACTION_COLOR[action] ?? 'var(--ink-soft)'
