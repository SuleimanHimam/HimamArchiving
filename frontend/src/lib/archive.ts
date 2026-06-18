import { api } from './api'

// PhysicalLocationType: 0 Building, 1 Room, 2 Cabinet, 3 Shelf, 4 Box
export type LocationType = 0 | 1 | 2 | 3 | 4

export interface PhysicalLocationDto {
  id: number
  parentId: number | null
  name: string
  type: string
  code: string | null
  rfidTag: string | null
  isActive: boolean
}

export interface PhysicalArchiveItemDto {
  id: number
  documentId: number | null
  incomingMailId: number | null
  physicalLocationId: number
  locationName: string
  boxNumber: string | null
  fileNumber: string | null
  archivedAt: string
  notes: string | null
}

export const archive = {
  locations: () => api.get<PhysicalLocationDto[]>('/physical-archive/locations').then((r) => r.data),

  createLocation: (body: { parentId?: number | null; name: string; type: LocationType; code?: string | null; rfidTag?: string | null }) =>
    api.post<PhysicalLocationDto>('/physical-archive/locations', body).then((r) => r.data),

  items: (locationId?: number) =>
    api.get<PhysicalArchiveItemDto[]>('/physical-archive/items', { params: { locationId } }).then((r) => r.data),

  createItem: (body: { documentId?: number | null; incomingMailId?: number | null; physicalLocationId: number; boxNumber?: string | null; fileNumber?: string | null; notes?: string | null }) =>
    api.post<PhysicalArchiveItemDto>('/physical-archive/items', body).then((r) => r.data),
}

export const LOCATION_TYPE_LABELS = ['مبنى', 'غرفة', 'خزانة', 'رف', 'صندوق']
