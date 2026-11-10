import type { LucideIcon } from 'lucide-react'
import {
  LayoutDashboard, Mail, PenSquare, FileText, Building2, Network, Archive,
  Recycle, ScrollText, UsersRound, DatabaseBackup, Settings, ScanLine, LayoutTemplate, GitBranch,
} from 'lucide-react'
import { api } from './api'

export interface NavSettingsResponse { hidden: string[] }

export const navSettingsApi = {
  get: () => api.get<NavSettingsResponse>('/nav-settings').then((r) => r.data.hidden ?? []),
  update: (hidden: string[]) => api.put<NavSettingsResponse>('/nav-settings', { hidden }).then((r) => r.data.hidden ?? []),
}

/** One entry in the sidebar. `perm` null means everyone sees it. */
export interface NavItem {
  key: string
  to: string
  labelKey: string
  /** A lucide component, not a character: the rail used assorted Unicode geometry
      (◈ ▤ ⚇ ⊡ ◔ ⛃) whose weight and size differed per glyph and per platform. */
  Icon: LucideIcon
  /** Match the route exactly (only the home entry needs this). */
  end?: boolean
  perm: string | null
  group: 'workspace' | 'records' | 'oversight' | 'system'
}

/**
 * The sidebar, in render order — the single source of truth.
 *
 * This list used to exist twice: once in AppLayout for rendering, once here as the
 * hideable-sections list. They drifted, and the settings panel ended up offering to hide
 * "البريد الوارد" and "البريد الصادر" long after those modules became الديوان and المراسلات,
 * while the modules that replaced them could not be hidden at all. Both the sidebar and the
 * settings panel now read this array, so a module added in one place shows up in the other.
 */
export const NAV_ITEMS: NavItem[] = [
  { key: 'home',        to: '/app',             labelKey: 'nav.home',           Icon: LayoutDashboard, end: true, perm: null,       group: 'workspace' },
  { key: 'diwan',       to: '/app/diwan',       labelKey: 'nav.diwan',          Icon: Mail, perm: 'Correspondence.View', group: 'records' },
  { key: 'internal',    to: '/app/internal',    labelKey: 'nav.correspondence', Icon: PenSquare, perm: 'Correspondence.View', group: 'records' },
  { key: 'documents',   to: '/app/documents',   labelKey: 'nav.documents',      Icon: FileText, perm: 'Documents.View',      group: 'records' },
  { key: 'external',    to: '/app/external',    labelKey: 'nav.external',       Icon: Building2, perm: 'Organization.View',   group: 'records' },
  { key: 'departments', to: '/app/departments', labelKey: 'nav.departments',    Icon: Network, perm: 'Organization.View',   group: 'records' },
  { key: 'archive',     to: '/app/archive',     labelKey: 'nav.archive',        Icon: Archive, perm: 'Archive.View',        group: 'records' },
  { key: 'disposition', to: '/app/disposition', labelKey: 'nav.disposition',    Icon: Recycle, perm: 'Disposition.View',    group: 'oversight' },
  // Despite the "monitoring" route name, this is the M7 audit log viewer, so Audit.View is the
  // right gate. Spec §18 separates M24 Observability (health checks, OCR queue depth, API p95,
  // backup-verify streak) from the audit log as a distinct subsystem — M24 has no screen yet,
  // and when it lands it needs its own route and its own permission, not this one.
  { key: 'monitoring',  to: '/app/monitoring',  labelKey: 'nav.monitoring',     Icon: ScrollText, perm: 'Audit.View',          group: 'oversight' },
  // Routes are institution configuration, like roles or retention schedules — not a daily task —
  // so the designer sits with the other administration screens rather than beside المراسلات.
  { key: 'workflow',    to: '/app/workflow',    labelKey: 'nav.workflow',       Icon: GitBranch, perm: 'Workflow.View',       group: 'system' },
  { key: 'users',       to: '/app/users',       labelKey: 'nav.users',          Icon: UsersRound, perm: 'Users.View',          group: 'system' },
  // Devices attached to this machine, not institution configuration — see ScannerPage for
  // why it is a section rather than a Settings tab.
  { key: 'scanner',     to: '/app/scanner',     labelKey: 'nav.scanner',        Icon: ScanLine, perm: 'Scanner.View',        group: 'system' },
  // Everyone owns a personal design, so this is not an administrator's screen — see
  // DesignGalleryPage for how the gallery narrows to what each user may actually touch.
  { key: 'printDesign', to: '/app/print-design', labelKey: 'nav.printDesign',   Icon: LayoutTemplate, perm: 'PrintDesign.View', group: 'system' },
  // Backup used to be a tab inside Settings. It is an operational task, not a preference —
  // the person doing it is usually reacting to something, so it gets its own rail entry.
  { key: 'backup',      to: '/app/backup',      labelKey: 'nav.backup',         Icon: DatabaseBackup, perm: 'Backup.Edit',         group: 'system' },
  { key: 'settings',    to: '/app/settings',    labelKey: 'nav.settings',       Icon: Settings, perm: 'Settings.View',       group: 'system' },
]

/** Route segments that map back to a sidebar section, used to bounce hidden sections. */
export const NAV_SECTION_KEYS: string[] = NAV_ITEMS.map((n) => n.key)

/**
 * Sections an admin may hide. Settings is deliberately excluded: hiding it would remove the
 * only route back to this panel to restore anything.
 */
export const NAV_SECTIONS: { key: string; labelKey: string }[] =
  NAV_ITEMS.filter((n) => n.key !== 'settings').map(({ key, labelKey }) => ({ key, labelKey }))
