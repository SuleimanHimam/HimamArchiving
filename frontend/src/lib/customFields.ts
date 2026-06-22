import { api } from './api'

export type CustomFieldType = 0 | 1 | 2 | 3 // Text | Number | Date | Choice

export interface CustomFieldDef {
  id: number
  entityType: string
  fieldKey: string
  label: string
  fieldType: CustomFieldType
  options: string | null   // newline-separated choices
  searchable: boolean
  sortOrder: number
  isActive: boolean
}

export interface CustomValue { fieldId: number; value: string }

export const FIELD_TYPE_LABELS: Record<number, string> = {
  0: 'نص', 1: 'رقم', 2: 'تاريخ', 3: 'قائمة اختيار',
}

// Record types that can carry custom fields.
export const CF_ENTITIES: { key: string; label: string }[] = [
  { key: 'Document', label: 'الوثائق' },
  { key: 'IncomingMail', label: 'الوارد' },
  { key: 'OutgoingMail', label: 'الصادر' },
  { key: 'ArchiveItem', label: 'البنود المؤرشفة' },
]

export const customFields = {
  list: (entityType: string) =>
    api.get<CustomFieldDef[]>('/custom-fields', { params: { entityType } }).then((r) => r.data),
  create: (b: { entityType: string; label: string; fieldType: number; options?: string | null; searchable: boolean }) =>
    api.post<CustomFieldDef>('/custom-fields', b).then((r) => r.data),
  update: (id: number, b: { label: string; fieldType: number; options?: string | null; searchable: boolean; sortOrder: number; isActive: boolean }) =>
    api.put<CustomFieldDef>(`/custom-fields/${id}`, b).then((r) => r.data),
  remove: (id: number) => api.delete(`/custom-fields/${id}`),

  values: (entityType: string, entityId: number) =>
    api.get<CustomValue[]>(`/custom-fields/values/${entityType}/${entityId}`).then((r) => r.data),
  saveValues: (entityType: string, entityId: number, values: Record<number, string>) =>
    api.put(`/custom-fields/values/${entityType}/${entityId}`, { values }),
}

export function optionList(def: CustomFieldDef): string[] {
  return (def.options ?? '').split('\n').map((s) => s.trim()).filter(Boolean)
}
