import { api } from './api'

export interface WorklistItem {
  taskId: number
  workflowInstanceId: number
  entityType: string
  entityId: number
  definitionName: string
  stageName: string
  status: string
  dueAt: string
  isOverdue: boolean
  allowedActions: string // flags string, e.g. "Approve, Reject, Forward"
}

// WorkflowActionType flag values
export const WF_ACTION = {
  Approve: 1, Reject: 2, Forward: 4, Hold: 8, Return: 16, Comment: 32, Close: 64,
} as const

export const WF_ACTION_LABELS: { name: keyof typeof WF_ACTION; value: number; label: string; cls: string }[] = [
  { name: 'Approve', value: 1, label: 'اعتماد', cls: 'btn-primary' },
  { name: 'Reject', value: 2, label: 'رفض', cls: 'btn-ghost' },
  { name: 'Return', value: 16, label: 'إعادة', cls: 'btn-ghost' },
  { name: 'Close', value: 64, label: 'إغلاق', cls: 'btn-seal' },
]

export const workflow = {
  myTasks: () => api.get<WorklistItem[]>('/workflow/my-tasks').then((r) => r.data),

  act: (taskId: number, action: number, note?: string | null, forwardToPositionId?: number | null) =>
    api.post(`/workflow/tasks/${taskId}/actions`, {
      action, note: note ?? null, forwardToPositionId: forwardToPositionId ?? null,
    }).then((r) => r.data),
}

const ENTITY_LABELS: Record<string, string> = {
  Document: 'وثيقة', IncomingMail: 'وارد', OutgoingMail: 'صادر',
}
export const entityLabel = (t: string) => ENTITY_LABELS[t] ?? t
