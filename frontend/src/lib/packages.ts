import { api } from './api'

export interface PackageFile {
  attachmentId: number
  fileName: string
  contentType: string
  sizeBytes: number
  checksum: string | null
  algorithm: string
  kind: string
  pronomPuid: string | null
}

export interface InformationPackage {
  id: number
  type: string // SIP | AIP | DIP
  documentId: number
  fileCount: number
  totalBytes: number
  createdAt: string
  files: PackageFile[]
}

export interface Representation {
  attachmentId: number
  fileName: string
  formatName: string | null
  mimeType: string | null
  pronomPuid: string | null
  renderingNote: string | null
}

export interface DocumentPackages {
  documentId: number
  documentNumber: string
  title: string
  sip: InformationPackage | null
  aip: InformationPackage | null
  dips: InformationPackage[]
  representation: Representation[]
}

export interface DesignatedCommunity {
  name: string
  description: string | null
  renderingExpectations: string | null
}

export const packagesApi = {
  get: (docId: number) => api.get<DocumentPackages>(`/documents/${docId}/packages`).then((r) => r.data),

  // Download the AIP ZIP honoring the bearer token.
  exportAip: async (docId: number, documentNumber: string) => {
    const res = await api.get(`/documents/${docId}/packages/aip/export`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `AIP-${documentNumber}.zip`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
}

export const designatedCommunityApi = {
  get: () => api.get<DesignatedCommunity>('/preservation/designated-community').then((r) => r.data),
  update: (body: DesignatedCommunity) =>
    api.put<DesignatedCommunity>('/preservation/designated-community', body).then((r) => r.data),
}

export interface PreservationPolicy {
  name: string
  description: string | null
  targetPdfAConformance: string
  autoNormalizeOnIngest: boolean
  fixityAlgorithm: string
  fixityCadenceDays: number
  allowedPreservationFormats: string | null
}

export const preservationPolicyApi = {
  get: () => api.get<PreservationPolicy>('/preservation/policy').then((r) => r.data),
  update: (body: PreservationPolicy) => api.put<PreservationPolicy>('/preservation/policy', body).then((r) => r.data),
}
