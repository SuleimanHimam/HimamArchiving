import { api } from './api'
import type { Confidentiality } from './incomingMail'

export interface AdminRole { id: number; name: string; description: string | null }

export interface AdminUser {
  id: number
  fullName: string
  email: string
  jobTitle: string
  clearance: string
  isActive: boolean
  roles: string[]
  roleIds: number[]
}

export interface CreateUser {
  fullName: string
  email: string
  password: string
  jobTitle?: string | null
  clearance: Confidentiality
  orgUnitId?: number | null
  roleIds: number[]
}

export const usersApi = {
  list: () => api.get<AdminUser[]>('/users').then((r) => r.data),
  roles: () => api.get<AdminRole[]>('/users/roles').then((r) => r.data),
  create: (body: CreateUser) => api.post<AdminUser>('/users', body).then((r) => r.data),
  setRoles: (id: number, roleIds: number[]) =>
    api.put<AdminUser>(`/users/${id}/roles`, { roleIds }).then((r) => r.data),
  setActive: (id: number, isActive: boolean) =>
    api.post<AdminUser>(`/users/${id}/active`, { isActive }).then((r) => r.data),
  resetPassword: (id: number, newPassword: string) =>
    api.post(`/users/${id}/reset-password`, { newPassword }),
}

// Arabic labels for the seeded roles.
export const ROLE_LABELS: Record<string, string> = {
  'System Administrator': 'مدير النظام',
  Manager: 'مدير',
  'Archive Officer': 'موظف أرشيف',
  Employee: 'موظف',
}
