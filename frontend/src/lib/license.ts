import { api } from './api'

/** Mirrors LicenseState on the server. */
export type LicenseState =
  | 'Missing'
  | 'Invalid'
  | 'MachineMismatch'
  | 'Expired'
  | 'Grace'
  | 'Valid'

export interface LicenseStatus {
  state: LicenseState
  isUsable: boolean
  /** Fingerprint of the server this instance runs on — the value quoted when ordering a licence. */
  machineId: string
  licenseId: string | null
  customer: string | null
  edition: string | null
  issuedAt: string | null
  expiresAt: string | null
  daysRemaining: number | null
  maxUsers: number | null
  features: string[]
  message: string
}

export const licenseApi = {
  status: () => api.get<LicenseStatus>('/license/status').then((r) => r.data),
  machineId: () => api.get<{ machineId: string }>('/license/machine-id').then((r) => r.data.machineId),
  /** A rejected key is a 400 whose body is the status explaining why — nothing is installed. */
  install: (license: string) => api.post<LicenseStatus>('/license', { license }).then((r) => r.data),
}

/** Arabic wording for each state, used by the settings panel and the expiry banner. */
export const LICENSE_STATE_LABELS: Record<LicenseState, string> = {
  Valid: 'ترخيص ساري',
  Grace: 'انتهى الترخيص — ضمن مهلة السماح',
  Expired: 'انتهت صلاحية الترخيص',
  MachineMismatch: 'الترخيص صادر لجهاز آخر',
  Invalid: 'ترخيص غير صالح',
  Missing: 'لا يوجد ترخيص',
}
