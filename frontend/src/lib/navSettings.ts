import { api } from './api'

export interface NavSettingsResponse { hidden: string[] }

export const navSettingsApi = {
  get: () => api.get<NavSettingsResponse>('/nav-settings').then((r) => r.data.hidden ?? []),
  update: (hidden: string[]) => api.put<NavSettingsResponse>('/nav-settings', { hidden }).then((r) => r.data.hidden ?? []),
}

// Hideable navbar sections (the "settings" section is intentionally not hideable so an
// admin can always reach this panel to restore visibility). Keys match AppLayout nav items.
export const NAV_SECTIONS: { key: string; labelKey: string }[] = [
  { key: 'home', labelKey: 'nav.home' },
  { key: 'incoming', labelKey: 'nav.incoming' },
  { key: 'outgoing', labelKey: 'nav.outgoing' },
  { key: 'documents', labelKey: 'nav.documents' },
  { key: 'archive', labelKey: 'nav.archive' },
  { key: 'locations', labelKey: 'nav.locations' },
  { key: 'disposition', labelKey: 'nav.disposition' },
  { key: 'monitoring', labelKey: 'nav.monitoring' },
]
