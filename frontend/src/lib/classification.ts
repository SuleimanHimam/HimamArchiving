import { api } from './api'

export interface ClassificationType {
  id: number
  nameAr: string
  nameEn: string | null
  description: string | null
  color: string
  sortOrder: number
  isSystem: boolean
  isActive: boolean
  roleIds: number[]
}

export interface UpsertClassification {
  nameAr: string
  nameEn?: string | null
  description?: string | null
  color: string
  sortOrder: number
  isActive: boolean
}

export const classificationApi = {
  list: () => api.get<ClassificationType[]>('/classification-types').then((r) => r.data),
  create: (body: UpsertClassification) => api.post<ClassificationType>('/classification-types', body).then((r) => r.data),
  update: (id: number, body: UpsertClassification) => api.put<ClassificationType>(`/classification-types/${id}`, body).then((r) => r.data),
  setRoles: (id: number, roleIds: number[]) => api.put(`/classification-types/${id}/roles`, { roleIds }).then((r) => r.data),
  delete: (id: number) => api.delete(`/classification-types/${id}`),
}

// Preset colour swatches for the colour picker
export const COLOR_SWATCHES = [
  { hex: '#2f5d3a', label: 'أخضر (عام)' },
  { hex: '#1e3a8a', label: 'أزرق (داخلي)' },
  { hex: '#9a6312', label: 'عنبري (سري)' },
  { hex: '#9b2226', label: 'أحمر (سري للغاية)' },
  { hex: '#6b21a8', label: 'بنفسجي' },
  { hex: '#0f766e', label: 'أزرق مخضر' },
  { hex: '#7c3aed', label: 'بنفسجي فاتح' },
  { hex: '#374151', label: 'رمادي داكن' },
  { hex: '#6b7280', label: 'رمادي' },
  { hex: '#1d4ed8', label: 'أزرق ملكي' },
]
