import { api } from './api'

// 0 User · 1 OrgUnit
export type GroupMemberType = 0 | 1

export interface GroupMember {
  id: number
  memberType: string        // 'User' | 'OrgUnit'
  userId: number | null
  userName: string | null
  orgUnitId: number | null
  orgUnitName: string | null
}

export interface Group {
  id: number
  name: string
  description: string | null
  isActive: boolean
  memberCount: number
  members: GroupMember[]
}

export interface GroupMemberInput {
  memberType: GroupMemberType
  userId?: number | null
  orgUnitId?: number | null
}

export interface SaveGroup {
  name: string
  description?: string | null
  isActive: boolean
  members: GroupMemberInput[]
}

export const groups = {
  /** `memberOnly` returns just the groups the signed-in user belongs to (directly or through
   *  their department) — the set they are allowed to address. The server enforces the same rule
   *  when the message is sent, so this narrows the picker rather than being the guarantee. */
  list: (activeOnly = false, memberOnly = false) =>
    api.get<Group[]>('/groups', { params: { activeOnly, memberOnly } }).then((r) => r.data),
  get: (id: number) => api.get<Group>(`/groups/${id}`).then((r) => r.data),

  /** Groups a given user belongs to (needs Users.View). For your own, use list(true, true). */
  ofUser: (userId: number) => api.get<Group[]>(`/groups/of-user/${userId}`).then((r) => r.data),
  create: (b: SaveGroup) => api.post<Group>('/groups', b).then((r) => r.data),
  update: (id: number, b: SaveGroup) => api.put<Group>(`/groups/${id}`, b).then((r) => r.data),
  remove: (id: number) => api.delete(`/groups/${id}`),
}
