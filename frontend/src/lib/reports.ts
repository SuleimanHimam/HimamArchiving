import { api } from './api'

export interface StatusCount { status: string; count: number }

export interface AuditItem {
  id: number
  action: string
  entityType: string
  entityTitle: string
  createdAt: string
  userName: string | null
}

export interface DashboardSummary {
  totalDocuments: number
  totalIncoming: number
  totalOutgoing: number
  openWorkflowTasks: number
  overdueWorkflowTasks: number
  expiringSoon: number
  pendingDisposals: number
  unreadNotifications: number
  documentsByStatus: StatusCount[]
  incomingByStatus: StatusCount[]
  outgoingByStatus: StatusCount[]
  recentActivity: AuditItem[]
}

export interface OnlineUser {
  id: number
  fullName: string
  role: string | null
  lastSeenAt: string
}

export const reports = {
  dashboard: () => api.get<DashboardSummary>('/reports/dashboard').then((r) => r.data),
  onlineUsers: () => api.get<OnlineUser[]>('/users/online').then((r) => r.data),
}
