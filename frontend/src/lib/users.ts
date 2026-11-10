import { api } from './api'
import type { Confidentiality } from './shared'

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
  /** The department the user is filed under. */
  orgUnitId: number | null
  orgUnitName: string | null
  /** Further departments the system administrator put them in charge of. */
  extraOrgUnitIds: number[]
  extraOrgUnitNames: string[]
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

/** One person's permissions, split by where each part comes from. */
export interface UserPermissions {
  userId: number
  fullName: string
  /** From their roles — the same for everyone holding those roles, and not editable here. */
  rolePermissionCodes: string[]
  /** Granted to this person alone, on top of their roles. */
  extraPermissionCodes: string[]
  /** What the signed-in caller may add or remove. A head may only pass on what they hold, so this
   *  is narrower for them than for the administrator; anything outside it renders disabled. */
  grantableCodes: string[]
}

/** Sidebar sections hidden for one person. Presentation only — permissions still decide access. */
export interface UserNavSections {
  userId: number
  fullName: string
  /** Hidden for this person alone. */
  hidden: string[]
  /** Hidden institution-wide by the administrator; not editable from the user screen. */
  hiddenForEveryone: string[]
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

  /** Replaces the extra departments this user may act in. System administrator only —
   *  the server refuses it for anyone else, whatever their permissions. */
  setDepartments: (id: number, orgUnitIds: number[], note?: string) =>
    api.put<AdminUser>(`/users/${id}/departments`, { orgUnitIds, note: note ?? null }).then((r) => r.data),

  /** Sidebar sections hidden for one person, plus the ones hidden institution-wide. */
  getUserNavSections: (id: number) =>
    api.get<UserNavSections>(`/users/${id}/nav-sections`).then((r) => r.data),

  setUserNavSections: (id: number, hidden: string[]) =>
    api.put<UserNavSections>(`/users/${id}/nav-sections`, { hidden }).then((r) => r.data),

  /** Permissions granted to one person on top of their roles. */
  getUserPermissions: (id: number) =>
    api.get<UserPermissions>(`/users/${id}/permissions`).then((r) => r.data),

  /** Replaces the personal grants. Codes their roles already carry are ignored by the server. */
  setUserPermissions: (id: number, permissionCodes: string[], note?: string) =>
    api.put<UserPermissions>(`/users/${id}/permissions`, { permissionCodes, note: note ?? null })
      .then((r) => r.data),

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

/** Arabic label for a role name, falling back to the raw value for custom roles. */
export const roleLabel = (name: string | null | undefined): string =>
  (name && ROLE_LABELS[name]) || name || '—'

// Every resource the server's PermissionCatalog defines. A resource missing from this map still
// renders (the code falls back to the raw name), but it renders in English and lands in no group,
// so keep it in step with backend/src/Archiving.Infrastructure/Persistence/PermissionCatalog.cs.
export const RESOURCE_LABELS: Record<string, string> = {
  Correspondence: 'المراسلات (الديوان والداخلي)',
  Documents:      'الوثائق',
  Notes:          'الملاحظات',
  Export:         'التصدير',
  Archive:        'الأرشيف المادي',
  Preservation:   'سياسات الحفظ',
  Disposition:    'التخلص من السجلات',
  Destruction:    'الإتلاف',
  LegalHold:      'الحجز القانوني',
  Workflow:       'سير العمل والتصعيد',
  Audit:          'سجل المراقبة',
  Users:          'المستخدمون والأدوار',
  Organization:   'الهيكل التنظيمي والجهات',
  PrintDesign:    'تصاميم الطباعة (الترويسات)',
  Settings:       'إعدادات النظام',
  Classification: 'التصنيف السري',
  CustomFields:   'الحقول المخصصة',
  TableColumns:   'تخصيص أعمدة الجداول',
  Scanner:        'الأجهزة (طابعة / ماسح)',
  Backup:         'النسخ الاحتياطي',
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

// Resource groups — controls section headings, row order, and per-section column labels.
// The four groups together must cover every key in RESOURCE_LABELS: RolesAdmin renders rows
// group by group, so a resource listed in none of them is invisible in the matrix and can
// never be granted, however faithfully the server enforces it.
export const RESOURCE_GROUPS: { label: string; resources: string[]; actionLabels: Record<string, string> }[] = [
  {
    label: 'المراسلات والوثائق',
    resources: ['Correspondence', 'Documents', 'Notes', 'Export'],
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
    label: 'الحفظ والتخلص من السجلات',
    resources: ['Archive', 'Preservation', 'Disposition', 'Destruction', 'LegalHold'],
    actionLabels: {
      View:    'استعراض',
      Create:  'إدراج',
      Edit:    'تعديل',
      Delete:  'حذف',
      Approve: 'اعتماد',
      Forward: 'مشاركة',
      Print:   'طباعة',
      Archive: 'حفظ',
    },
  },
  {
    label: 'الرقابة والحوكمة',
    resources: ['Workflow', 'Audit'],
    actionLabels: {
      View:    'استعراض',
      Create:  'إنشاء',
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
    resources: ['Users', 'Organization', 'PrintDesign', 'Letterhead', 'Settings', 'Classification', 'CustomFields', 'TableColumns', 'Scanner', 'Backup'],
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
