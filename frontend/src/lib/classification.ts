import { api } from './api'
import { applyClassificationLabels } from './shared'

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

/** Pulls the admin-managed names into the shared label table. Called once per session by the
 *  app shell, and again after the settings screen saves, so a rename shows up without a reload.
 *
 *  Reads `/labels`, not the full catalogue: the catalogue is behind Classification.View, which
 *  most staff do not hold, while everyone sees classification badges. */
export async function syncClassificationLabels(): Promise<boolean> {
  try {
    const rows = await api.get<{ nameAr: string; sortOrder: number }[]>('/classification-types/labels')
    applyClassificationLabels(rows.data)
    return true
  } catch {
    return false   // keep the built-in names; a missing catalogue must not blank every badge
  }
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
