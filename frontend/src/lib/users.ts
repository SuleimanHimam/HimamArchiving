import { api } from './api'
import type { Confidentiality } from './incomingMail'

export interface AdminRole { id: number; name: string; description: string | null }

export interface AdminUser {
  id: number
  fullName: string
  firstName: string | null
  secondName: string | null
  thirdName: string | null
  familyName: string | null
  gender: 'Male' | 'Female' | 'NotSpecified'
  nationalId: string | null
  email: string
  jobTitle: string
  clearance: string
  isActive: boolean
  roles: string[]
  roleIds: number[]
}

export type Gender = 'NotSpecified' | 'Male' | 'Female'

export interface CreateUser {
  fullName: string
  firstName?: string | null
  secondName?: string | null
  thirdName?: string | null
  familyName?: string | null
  gender: 0 | 1 | 2   // NotSpecified=0 Male=1 Female=2
  nationalId?: string | null
  email: string
  password: string
  jobTitle?: string | null
  clearance: Confidentiality
  orgUnitId?: number | null
  roleIds: number[]
}

export interface PermissionInfo {
  code: string
  resource: string
  action: string
}

export interface RolePermissions {
  id: number
  name: string
  description: string | null
  isSystem: boolean
  permissionCodes: string[]
}

export const usersApi = {
  list: () => api.get<AdminUser[]>('/users').then((r) => r.data),
  roles: () => api.get<AdminRole[]>('/users/roles').then((r) => r.data),
  permissions: () => api.get<PermissionInfo[]>('/users/permissions').then((r) => r.data),

  create: (body: CreateUser) => api.post<AdminUser>('/users', body).then((r) => r.data),

  // Single role — pass array of one
  setRole: (id: number, roleId: number) =>
    api.put<AdminUser>(`/users/${id}/roles`, { roleIds: [roleId] }).then((r) => r.data),

  setActive: (id: number, isActive: boolean) =>
    api.post<AdminUser>(`/users/${id}/active`, { isActive }).then((r) => r.data),

  resetPassword: (id: number, newPassword: string) =>
    api.post(`/users/${id}/reset-password`, { newPassword }),

  // Role management
  getRolePermissions: (roleId: number) =>
    api.get<RolePermissions>(`/users/roles/${roleId}/permissions`).then((r) => r.data),

  setRolePermissions: (roleId: number, permissionCodes: string[]) =>
    api.put<RolePermissions>(`/users/roles/${roleId}/permissions`, { permissionCodes }).then((r) => r.data),

  resetRolePermissions: (roleId: number) =>
    api.post<RolePermissions>(`/users/roles/${roleId}/permissions/reset`).then((r) => r.data),

  createRole: (name: string, description?: string) =>
    api.post<AdminRole>('/users/roles', { name, description: description || null }).then((r) => r.data),

  deleteRole: (roleId: number) =>
    api.delete(`/users/roles/${roleId}`),
}

export const ROLE_LABELS: Record<string, string> = {
  'System Administrator': 'مدير النظام',
  Manager: 'مدير',
  'Archive Officer': 'موظف أرشيف',
  Employee: 'موظف',
}

export const RESOURCE_LABELS: Record<string, string> = {
  Documents:      'الوثائق',
  IncomingMail:   'البريد الوارد',
  OutgoingMail:   'البريد الصادر',
  Workflow:       'سير العمل',
  Archive:        'الأرشيف المادي',
  Reports:        'التقارير',
  Audit:          'سجل المراقبة',
  Users:          'المستخدمون',
  Organization:   'الهيكل التنظيمي',
  Classification: 'التصنيف السري',
  Preservation:   'سياسات الحفظ',
  Backup:         'النسخ الاحتياطي',
  Scanner:        'الأجهزة (طابعة / ماسح)',
  CustomFields:   'الحقول المخصصة',
  Notes:          'الملاحظات',
  Export:         'التصدير',
  TableColumns:   'تخصيص أعمدة الجداول',
  Destruction:    'الإتلاف',
  LegalHold:      'الحجز القانوني',
}

export const ACTION_LABELS: Record<string, string> = {
  View:    'عرض',
  Create:  'إضافة',
  Edit:    'تعديل',
  Delete:  'حذف',
  Approve: 'اعتماد',
  Forward: 'إحالة',
  Print:   'طباعة',
  Archive: 'أرشفة',
}

// Logical display order for action columns
export const ACTION_ORDER = ['View', 'Create', 'Edit', 'Delete', 'Approve', 'Forward', 'Print', 'Archive']

// Resource groups — controls section headings, row order, and per-section column labels
export const RESOURCE_GROUPS: { label: string; resources: string[]; actionLabels: Record<string, string> }[] = [
  {
    label: 'المراسلات والوثائق',
    resources: ['IncomingMail', 'OutgoingMail', 'Documents', 'Workflow', 'Notes', 'Export'],
    actionLabels: {
      View:    'استعراض',
      Create:  'تسجيل',
      Edit:    'تعديل',
      Delete:  'حذف',
      Approve: 'اعتماد',
      Forward: 'إحالة',
      Print:   'طباعة',
      Archive: 'أرشفة',
    },
  },
  {
    label: 'الحفظ والرقابة',
    resources: ['Archive', 'Reports', 'Audit', 'LegalHold', 'Destruction'],
    actionLabels: {
      View:    'استعراض',
      Create:  'إدراج',
      Edit:    'تعديل',
      Delete:  'حذف',
      Approve: 'مراجعة',
      Forward: 'مشاركة',
      Print:   'طباعة',
      Archive: 'حفظ',
    },
  },
  {
    label: 'إدارة النظام',
    resources: ['Users', 'Organization', 'Classification', 'Preservation', 'Backup', 'Scanner', 'CustomFields', 'TableColumns'],
    actionLabels: {
      View:    'استعراض',
      Create:  'إنشاء',
      Edit:    'تعديل',
      Delete:  'حذف',
      Approve: 'تفعيل',
      Forward: 'تفويض',
      Print:   'طباعة',
      Archive: 'نسخ',
    },
  },
]
