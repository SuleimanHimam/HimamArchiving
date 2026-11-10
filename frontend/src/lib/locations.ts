import { api } from './api'

export interface Building { id: number; nameAr: string; nameEn: string | null; code: string | null; address: string | null; notes: string | null; isActive: boolean; roomCount: number }
export interface Room { id: number; buildingId: number; buildingName: string; nameAr: string; nameEn: string | null; roomNumber: string | null; floor: string | null; notes: string | null; isActive: boolean; cabinetCount: number }
export interface RoomConnection { id: number; roomId: number; connectedRoomId: number; connectedRoomName: string; connectionType: string | null; notes: string | null }
export interface Cabinet { id: number; roomId: number; roomName: string; nameAr: string; nameEn: string | null; cabinetCode: string | null; shelfCount: number; notes: string | null; isActive: boolean; shelvesActual: number }
export interface Shelf { id: number; cabinetId: number; cabinetName: string; shelfNumber: string; capacity: number | null; notes: string | null; isActive: boolean; boxCount: number }
export interface Box { id: number; shelfId: number | null; roomId: number | null; boxCode: string; barcode: string | null; capacity: number | null; currentCount: number; isFull: boolean; notes: string | null; isActive: boolean }
/** The five levels of the physical hierarchy, from the building down to the box a document sits in. */
export type LocationKind = 'Building' | 'Room' | 'Cabinet' | 'Shelf' | 'Box'
export const LOCATION_KIND_LABELS: Record<LocationKind, string> = {
  Building: 'مبنى', Room: 'غرفة', Cabinet: 'خزانة', Shelf: 'رف', Box: 'صندوق',
}

export interface TreeNode { id: number; type: string; name: string; code: string | null; children: TreeNode[] }

/** One place per row, depth-first, each carrying the full path to it ("مبنى › غرفة ٣ › ..."),
 *  so the tree can be offered as a flat list of choices without losing where each one sits. */
export interface PlaceOption { kind: LocationKind; id: number; name: string; path: string; depth: number }

export function flattenPlaces(nodes: TreeNode[], trail = '', depth = 0): PlaceOption[] {
  const out: PlaceOption[] = []
  for (const n of nodes) {
    const path = trail ? `${trail} › ${n.name}` : n.name
    out.push({ kind: n.type as LocationKind, id: n.id, name: n.name, path, depth })
    out.push(...flattenPlaces(n.children, path, depth + 1))
  }
  return out
}
export interface Breadcrumb { boxId: number; path: string; locationCode: string; parts: string[] }
export interface LocationAncestry { boxId: number; shelfId: number | null; cabinetId: number | null; roomId: number | null; buildingId: number | null }

export const CONNECTION_TYPES = ['Door', 'Corridor', 'Internal Passage']
export const CONNECTION_LABELS: Record<string, string> = { Door: 'باب', Corridor: 'ممر', 'Internal Passage': 'ممر داخلي' }

export const locations = {
  buildings: () => api.get<Building[]>('/buildings').then((r) => r.data),
  createBuilding: (b: Record<string, unknown>) => api.post<Building>('/buildings', b).then((r) => r.data),
  updateBuilding: (id: number, b: Record<string, unknown>) => api.put<Building>(`/buildings/${id}`, b).then((r) => r.data),
  deleteBuilding: (id: number) => api.delete(`/buildings/${id}`),

  rooms: (buildingId?: number) => api.get<Room[]>('/rooms', { params: { buildingId } }).then((r) => r.data),
  createRoom: (b: Record<string, unknown>) => api.post<Room>('/rooms', b).then((r) => r.data),
  updateRoom: (id: number, b: Record<string, unknown>) => api.put<Room>(`/rooms/${id}`, b).then((r) => r.data),
  deleteRoom: (id: number) => api.delete(`/rooms/${id}`),

  connections: (roomId: number) => api.get<RoomConnection[]>(`/rooms/${roomId}/connections`).then((r) => r.data),
  addConnection: (roomId: number, b: { connectedRoomId: number; connectionType?: string | null; notes?: string | null }) =>
    api.post<RoomConnection>(`/rooms/${roomId}/connections`, b).then((r) => r.data),
  removeConnection: (roomId: number, connectionId: number) => api.delete(`/rooms/${roomId}/connections/${connectionId}`),

  cabinets: (roomId?: number) => api.get<Cabinet[]>('/cabinets', { params: { roomId } }).then((r) => r.data),
  createCabinet: (b: Record<string, unknown>) => api.post<Cabinet>('/cabinets', b).then((r) => r.data),
  updateCabinet: (id: number, b: Record<string, unknown>) => api.put<Cabinet>(`/cabinets/${id}`, b).then((r) => r.data),
  deleteCabinet: (id: number) => api.delete(`/cabinets/${id}`),

  shelves: (cabinetId?: number) => api.get<Shelf[]>('/shelves', { params: { cabinetId } }).then((r) => r.data),
  createShelf: (b: Record<string, unknown>) => api.post<Shelf>('/shelves', b).then((r) => r.data),
  updateShelf: (id: number, b: Record<string, unknown>) => api.put<Shelf>(`/shelves/${id}`, b).then((r) => r.data),
  deleteShelf: (id: number) => api.delete(`/shelves/${id}`),

  boxes: (params: { shelfId?: number; roomId?: number }) => api.get<Box[]>('/boxes', { params }).then((r) => r.data),
  createBox: (b: Record<string, unknown>) => api.post<Box>('/boxes', b).then((r) => r.data),
  updateBox: (id: number, b: Record<string, unknown>) => api.put<Box>(`/boxes/${id}`, b).then((r) => r.data),
  deleteBox: (id: number) => api.delete(`/boxes/${id}`),

  tree: () => api.get<TreeNode[]>('/locations/tree').then((r) => r.data),
  breadcrumb: (boxId: number) => api.get<Breadcrumb>(`/locations/${boxId}/breadcrumb`).then((r) => r.data),
  ancestry: (boxId: number) => api.get<LocationAncestry>(`/locations/${boxId}/ancestry`).then((r) => r.data),
  migrateLegacy: () => api.post<{ message: string }>('/locations/migrate-legacy', {}).then((r) => r.data),
}
