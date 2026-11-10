import { api } from './api'

/** Which axis the tree branches on — mirrors the backend ArchiveTreeMode enum. */
export type ArchiveTreeMode = 'Department' | 'External'

export type ArchiveNodeKind = 'Department' | 'Year' | 'DocumentType' | 'ExternalOrg' | 'Direction' | 'Internal'

export interface ArchiveTreeNodeDto {
  /** Parseable selector (e.g. `dept:5|y:2026|t:3`) — hand it straight back to `documents()`. */
  id: string
  name: string
  kind: ArchiveNodeKind
  /** Documents here, descendants included. */
  count: number
  code: string | null
  children: ArchiveTreeNodeDto[] | null
}

export interface ArchiveTreeDocumentDto {
  id: number
  documentNumber: string
  title: string
  documentTypeName: string
  departmentName: string
  confidentiality: string
  status: string
  documentDate: string | null
  createdAt: string
  externalOrgName: string | null
  direction: 'Incoming' | 'Outgoing' | null
  /** Which register the document was filed from — both registers write into the same table. */
  origin: 'External' | 'Internal' | 'Direct'
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export const archiveTree = {
  tree: (mode: ArchiveTreeMode) =>
    api.get<ArchiveTreeNodeDto[]>('/archive/tree', { params: { mode } }).then((r) => r.data),

  /** Documents under `node` (omit for the whole archive). */
  documents: (node: string | null, search: string, page = 1, pageSize = 20) =>
    api
      .get<PagedResult<ArchiveTreeDocumentDto>>('/archive/tree/documents', {
        params: { node: node || undefined, search: search || undefined, page, pageSize },
      })
      .then((r) => r.data),
}
