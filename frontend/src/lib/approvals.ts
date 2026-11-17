import { api } from './api'

/** One open approval task (صندوق الموافقات). Decisions post to the workflow task endpoint by taskId. */
export interface ApprovalInboxItem {
  taskId: number
  requestId: number
  number: string
  requestType: string
  sourceModule: string
  sourceId: number
  title: string
  requesterName: string | null
  submittedAt: string
  stageName: string
  dueAt: string
  isOverdue: boolean
  allowedActions: string
}

export const approvalsApi = {
  inbox: () => api.get<ApprovalInboxItem[]>('/core/approvals/inbox').then((r) => r.data),
}
