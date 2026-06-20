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

export interface ChainVerifyResult {
  valid: boolean
  checkedCount: number
  firstBrokenId?: number | null
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

  verify: () =>
    api.get<ChainVerifyResult>('/audit/verify').then((r) => r.data),
}

// Arabic labels for known action codes
export const ACTION_AR: Record<string, string> = {
  View:    'عرض',
  Create:  'إنشاء',
  Edit:    'تعديل',
  Delete:  'حذف',
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
}

export const ACTION_COLOR: Record<string, string> = {
  Create:  'var(--brass)',
  Edit:    '#1e3a8a',
  Delete:  'var(--seal)',
  Forward: '#0f766e',
  Approve: '#2f5d3a',
  Archive: '#374151',
  Print:   '#7c3aed',
  Login:   '#6b21a8',
}
