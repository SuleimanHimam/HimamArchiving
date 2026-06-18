import { api } from './api'

export interface RecordAgent { id: number; agentKind: string; agentId: number; agentName: string; role: string }
export interface RecordRelationship {
  id: number; sourceType: string; sourceId: number; targetType: string; targetId: number; type: string; targetTitle: string | null
}
export interface RecordActivity { workflowInstanceId: number; definitionName: string; status: string; startedAt: string }
export interface RecordMetadata {
  documentId: number
  documentNumber: string
  title: string
  agents: RecordAgent[]
  relationships: RecordRelationship[]
  activities: RecordActivity[]
}

export const metadataApi = {
  get: (docId: number) => api.get<RecordMetadata>(`/documents/${docId}/metadata`).then((r) => r.data),
}

export const AGENT_ROLE_LABELS: Record<string, string> = {
  Creator: 'منشئ', Owner: 'مالك', Custodian: 'الجهة الحافظة', Contributor: 'مساهم', Approver: 'معتمد', Recipient: 'مستلم',
}
export const AGENT_KIND_LABELS: Record<string, string> = { User: 'مستخدم', Position: 'منصب', OrgUnit: 'وحدة' }
export const REL_TYPE_LABELS: Record<string, string> = {
  IsVersionOf: 'إصدار من', References: 'يشير إلى', Supersedes: 'يحل محل', RespondsTo: 'رد على', PartOf: 'جزء من', Attachment: 'مرفق',
}
