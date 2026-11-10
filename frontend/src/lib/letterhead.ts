import { api } from './api'

/** Slots the institution owns. Locked — only an Organization Admin may change them. */
export interface OrgSlots {
  logoBase64: string | null
  brandPrimaryHex: string
  brandAccentHex: string
  /** The only accent colours a department may pick from. */
  approvedPalette: string[]
  legalFooterAr: string | null
  legalFooterEn: string | null
  marginTopMm: number
  marginBottomMm: number
  marginInnerMm: number
  marginOuterMm: number
  baseFontFamily: string
  baseFontPt: number
  pageSize: string
}

/** What a department may change — deliberately two fields. Edits require approval. */
export interface DepartmentSlots {
  departmentLine: string | null
  accentHex: string | null
}

/** What an employee may change on their own letters: identity only. */
export interface SignerSlots {
  signerName: string | null
  signerTitle: string | null
  signerContact: string | null
}

export interface LetterheadProfile {
  org: OrgSlots
  department: DepartmentSlots | null
  signer: SignerSlots | null
  orgUnitId: number | null
  canEditOrg: boolean
  canEditDepartment: boolean
  pendingApproval: boolean
}

/** A legacy design the migration could not map — kept for manual re-authoring, never deleted. */
export interface LetterheadReviewItem {
  id: number
  name: string
  scope: string
  orgUnitName: string | null
  userName: string | null
  reviewNote: string | null
  createdAt: string
}

/** Letter content for a preview. Body is plain text — the master layout takes no markup. */
export interface LetterContent {
  registerNumber: string
  subject: string
  mailDate: string
  sentDate: string | null
  recipientOrg: string | null
  addresseeName: string | null
  addresseeTitle: string | null
  body: string
  classification: string | null
  fileNumber: string | null
}

export interface PreviewRequest {
  org?: OrgSlots | null
  department?: DepartmentSlots | null
  signer?: SignerSlots | null
  /** Omit to preview the letterhead with sample text (the designer's case). */
  content?: LetterContent | null
  orgUnitId?: number | null
}

export const letterhead = {
  profile: (orgUnitId?: number | null) =>
    api.get<LetterheadProfile>('/letterhead/profile', { params: { orgUnitId } }).then((r) => r.data),

  saveOrg: (slots: OrgSlots) =>
    api.put<OrgSlots>('/letterhead/org', { slots }).then((r) => r.data),

  saveDepartment: (orgUnitId: number, slots: DepartmentSlots) =>
    api.put<{ slots: DepartmentSlots; pendingApproval: boolean }>(
      `/letterhead/department/${orgUnitId}`, { slots }).then((r) => r.data),

  saveSigner: (slots: SignerSlots) =>
    api.put<SignerSlots>('/letterhead/user', { slots }).then((r) => r.data),

  review: () => api.get<LetterheadReviewItem[]>('/letterhead/review').then((r) => r.data),

  /**
   * The live preview. Returns an object URL for the PDF the *real* renderer produced — this is
   * not a CSS approximation of the layout, it is the same bytes the printer would get, so what
   * the designer shows cannot drift from the final letter.
   * Callers must revokeObjectURL when they are done.
   */
  previewUrl: async (body: PreviewRequest): Promise<string> => {
    const res = await api.post('/letterhead/preview', body, { responseType: 'blob' })
    return URL.createObjectURL(new Blob([res.data as Blob], { type: 'application/pdf' }))
  },
}

/** The rendered PDF of a saved outgoing letter — the only way one reaches paper. */
export const diwanLetterPdfUrl = async (id: number): Promise<string> => {
  const res = await api.get(`/diwan/${id}/letter.pdf`, { responseType: 'blob' })
  return URL.createObjectURL(new Blob([res.data as Blob], { type: 'application/pdf' }))
}
