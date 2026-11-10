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

/* ── Diwan dashboard: per-bureau tiles + real month-bucketed series ── */

export interface BureauMailStat {
  orgUnitId: number | null
  name: string
  incoming: number
  outgoing: number
}

export interface MonthlyBureauPoint {
  month: number
  orgUnitId: number | null
  incoming: number
  outgoing: number
}

export interface MonthlyCount {
  month: number
  count: number
}

export interface DashboardDepartment {
  id: number
  name: string
}

export interface DiwanDashboard {
  year: number
  totalDocuments: number
  bureaus: BureauMailStat[]
  monthlyMail: MonthlyBureauPoint[]
  monthlyAttachments: MonthlyCount[]
  availableYears: number[]
  /** Departments this user may narrow the charts to; one entry or none means no filter is offered. */
  departments: DashboardDepartment[]
  /** The department currently applied, echoed by the server — null is the whole institution. */
  orgUnitId: number | null
}

export const reports = {
  dashboard: () => api.get<DashboardSummary>('/reports/dashboard').then((r) => r.data),
  onlineUsers: () => api.get<OnlineUser[]>('/users/online').then((r) => r.data),
  diwanDashboard: (year?: number, orgUnitId?: number | null) =>
    api.get<DiwanDashboard>('/reports/diwan-dashboard', {
      params: { year: year || undefined, orgUnitId: orgUnitId || undefined },
    }).then((r) => r.data),
}
