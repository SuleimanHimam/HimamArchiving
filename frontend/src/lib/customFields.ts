import { api } from './api'

export type CustomFieldType = 0 | 1 | 2 | 3 // Text | Number | Date | Choice

export interface CustomFieldDef {
  id: number
  entityType: string
  /** Narrowed to one document type, or null when shared by every type of this record. */
  documentTypeId: number | null
  documentTypeName: string | null
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
  /**
   * Field definitions for a record type. Pass `documentTypeId` to get what a record of that type
   * actually carries — the type's own fields plus the shared ones — or `sharedOnly` for just the
   * shared ones. With neither, every field defined for the record type.
   */
  list: (entityType: string, opts?: { documentTypeId?: number | null; sharedOnly?: boolean }) =>
    api.get<CustomFieldDef[]>('/custom-fields', {
      params: {
        entityType,
        documentTypeId: opts?.documentTypeId ?? undefined,
        sharedOnly: opts?.sharedOnly ? true : undefined,
      },
    }).then((r) => r.data),
  create: (b: { entityType: string; label: string; fieldType: number; options?: string | null; searchable: boolean; documentTypeId?: number | null }) =>
    api.post<CustomFieldDef>('/custom-fields', b).then((r) => r.data),
  update: (id: number, b: { label: string; fieldType: number; options?: string | null; searchable: boolean; sortOrder: number; isActive: boolean; documentTypeId?: number | null }) =>
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
