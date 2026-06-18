import { api } from './api'

export interface StatusCount { status: string; count: number }

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
}

export const reports = {
  dashboard: () => api.get<DashboardSummary>('/reports/dashboard').then((r) => r.data),
}
