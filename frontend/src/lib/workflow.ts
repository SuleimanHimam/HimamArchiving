import { api } from './api'

/* The workflow engine's client. There is no workflow inbox: a run reaches people through المراسلات,
   so what lives here is the route configuration, the caller's task list, and the strip a piece of
   correspondence shows about the run it belongs to. */

// WorkflowActionType is a [Flags] enum on the server; a stage's AllowedActions is a comma-joined
// string of the names, which is what `stageAllows` below parses.
export const WF_ACTION = {
  Approve: 1, Reject: 2, Forward: 4, Hold: 8, Return: 16, Comment: 32, Close: 64, RequestChanges: 128,
} as const

export type WfActionName = keyof typeof WF_ACTION

export const WF_ACTIONS: { name: WfActionName; value: number; label: string; cls: string; needsNote: boolean }[] = [
  { name: 'Approve', value: 1, label: 'اعتماد', cls: 'btn-primary', needsNote: false },
  { name: 'RequestChanges', value: 128, label: 'إعادة للتعديل', cls: 'btn-ghost', needsNote: true },
  { name: 'Return', value: 16, label: 'إرجاع للمرحلة السابقة', cls: 'btn-ghost', needsNote: false },
  { name: 'Reject', value: 2, label: 'رفض', cls: 'btn-seal', needsNote: true },
  { name: 'Close', value: 64, label: 'إغلاق', cls: 'btn-ghost', needsNote: false },
]

/** True when a stage's AllowedActions string ("Approve, Reject, Comment") includes this action. */
export function stageAllows(allowedActions: string | null | undefined, name: WfActionName): boolean {
  if (!allowedActions) return false
  return allowedActions.split(',').map((s) => s.trim()).includes(name)
}

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
  allowedActions: string
}

export interface WorkflowTimelineEntry {
  at: string
  kind: 'Submitted' | 'Sent' | 'Approved' | 'Rejected' | 'ChangesRequested' | 'Returned' | 'Completed' | string
  stageName: string
  actorName: string | null
  note: string | null
  internalMailId: number | null
}

/** The strip a correspondence view shows: where the run is, and whether it waits on this reader. */
export interface WorkflowMailContext {
  instanceId: number
  workflowDefinitionId: number
  definitionName: string
  status: 'Running' | 'Completed' | 'Rejected' | 'Cancelled' | 'ChangesRequested' | string
  rootInternalMailId: number
  currentStageName: string | null
  stepNumber: number
  totalSteps: number
  mailRole: 'Origin' | 'Step' | 'Result' | null
  actionRequired: boolean
  myTaskId: number | null
  myTaskAllowedActions: string | null
  myTaskDueAt: string | null
  canResubmit: boolean
  timeline: WorkflowTimelineEntry[]
}

/** Workflow columns on an inbox row. */
export interface WorkflowMailBadge {
  instanceId: number
  definitionName: string
  status: string
  currentStageName: string | null
  stepNumber: number
  totalSteps: number
  actionRequired: boolean
  myTaskId: number | null
  mailRole: string | null
}

// ---- Definitions (admin designer) ----

export interface WorkflowDefinitionListItem {
  id: number
  name: string
  triggerModule: string
  version: number
  isActive: boolean
  stageCount: number
  mailCategoryId: number | null
  mailCategoryName: string | null
}

export interface WorkflowStageDto {
  id: number
  name: string
  order: number
  assigneeType: string
  assigneePositionId: number | null
  assigneeOrgUnitId: number | null
  assigneeUserId: number | null
  responseHours: number
  escalateAfterHours: number | null
  escalateToPositionId: number | null
  allowedActions: string
  isFinal: boolean
  type: string
  conditionExpression: string | null
  parallelGroupId: number | null
  requiresAllParallelComplete: boolean
  assignmentType: string
  assignmentExpression: string | null
  requiresCorrespondence: boolean
  rejectBehavior: string
  returnResume: string
  minimumApprovals: number | null
  /** Palette key the administrator painted the stage with; null = the default tile. */
  color: string | null
}

export interface WorkflowDefinitionDto {
  id: number
  name: string
  description: string | null
  triggerModule: string
  documentTypeId: number | null
  mailCategoryId: number | null
  version: number
  isActive: boolean
  stages: WorkflowStageDto[]
}

/** Numeric enum values as the API expects them on create. */
export const ASSIGNEE_TYPE = { Position: 0, OrgUnit: 1, Role: 2, User: 3, DirectManager: 4 } as const
export const STAGE_TYPE = { Sequential: 0, Parallel: 1, Conditional: 2 } as const
export const ASSIGNMENT_TYPE = {
  Static: 0, OrgHierarchy: 1, DocumentOwnerDepartment: 2, DepartmentHead: 3,
} as const
export const REJECT_BEHAVIOR = { StopAndReturnToRequester: 0, ReturnToPreviousStage: 1 } as const
export const RETURN_RESUME = { ResumeAtSameStage: 0, RestartFromStart: 1 } as const

export interface CreateStageRequest {
  name: string
  order: number
  assigneeType: number
  assigneePositionId?: number | null
  assigneeOrgUnitId?: number | null
  assigneeUserId?: number | null
  responseHours: number
  escalateAfterHours?: number | null
  escalateToPositionId?: number | null
  allowedActions: number
  isFinal: boolean
  type?: number
  conditionExpression?: string | null
  parallelGroupId?: number | null
  requiresAllParallelComplete?: boolean
  assignmentType?: number
  assignmentExpression?: string | null
  rejectBehavior?: number
  returnResume?: number
  minimumApprovals?: number | null
  color?: string | null
}

export interface CreateDefinitionRequest {
  name: string
  description?: string | null
  triggerModule: string
  documentTypeId?: number | null
  mailCategoryId?: number | null
  isActive?: boolean
  stages: CreateStageRequest[]
}

export const workflow = {
  // Definitions
  definitions: () => api.get<WorkflowDefinitionListItem[]>('/workflow/definitions').then((r) => r.data),
  definition: (id: number) => api.get<WorkflowDefinitionDto>(`/workflow/definitions/${id}`).then((r) => r.data),
  createDefinition: (body: CreateDefinitionRequest) =>
    api.post<WorkflowDefinitionDto>('/workflow/definitions', body).then((r) => r.data),

  // Runs
  instance: (id: number) => api.get(`/workflow/instances/${id}`).then((r) => r.data),
  resubmit: (instanceId: number, note?: string) =>
    api.post(`/workflow/instances/${instanceId}/resubmit`, { note: note ?? null }).then((r) => r.data),

  // Tasks
  myTasks: () => api.get<WorklistItem[]>('/workflow/my-tasks').then((r) => r.data),
  act: (taskId: number, action: number, note?: string | null, forwardToPositionId?: number | null) =>
    api.post(`/workflow/tasks/${taskId}/actions`,
      { action, note: note ?? null, forwardToPositionId: forwardToPositionId ?? null }).then((r) => r.data),

  /** The run a piece of correspondence belongs to. Resolves null for ordinary mail (204). */
  forMail: (internalMailId: number) =>
    api.get<WorkflowMailContext | ''>(`/workflow/for-mail/${internalMailId}`)
      .then((r) => (r.status === 204 || !r.data ? null : (r.data as WorkflowMailContext))),
}
