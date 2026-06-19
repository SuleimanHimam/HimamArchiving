import { api } from './api'

export interface PublicStats {
  todayTransactions: number
  pendingApproval: number
  overdue: number
}

export const publicStats = {
  get: () => api.get<PublicStats>('/public/stats').then((r) => r.data),
}
