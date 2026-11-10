import { useState, useEffect, useSyncExternalStore } from 'react'
import { Menu } from 'lucide-react'
import { Link, NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import type { LucideIcon } from 'lucide-react'
import { auth } from '../lib/auth'
import { endSession } from '../lib/session'
import { useConfirmClose } from '../lib/useConfirmClose'
import { useCurrentUser, useIsSystemAdmin } from '../lib/useCurrentUser'
import { getBranding, subscribeBranding } from '../lib/branding'
import { navSettingsApi, NAV_ITEMS, NAV_SECTION_KEYS } from '../lib/navSettings'
import { loadTablePrefs, loadCustomFieldCols } from '../lib/tableColumns'
import { syncClassificationLabels } from '../lib/classification'
import NotificationBell from './NotificationBell'
import AccountMenu from './AccountMenu'
import LanguageToggle from './LanguageToggle'
import InstallAppButton from './InstallAppButton'
import IdleTimeout from './IdleTimeout'
import BrandMark from './BrandMark'
import ThemeToggle from './ThemeToggle'
import GlobalDocumentSearch from './GlobalDocumentSearch'
import './applayout.css'

// Maps a path under /app to its navbar section key (or null for unknown paths).
function sectionKeyForPath(pathname: string): string | null {
  const m = pathname.match(/^\/app(?:\/([^/]+))?/)
  if (!m) return null
  const seg = m[1]
  if (!seg) return 'home'
  return NAV_SECTION_KEYS.includes(seg) ? seg : null
}

export default function AppLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const qc = useQueryClient()
  const { data: user } = useCurrentUser()
  const [menuOpen, setMenuOpen] = useState(false)
  const [createMenuOpen, setCreateMenuOpen] = useState(false)
  // Rail collapse (252px ⇄ 68px) is gone with the desktop header toggle that drove
  // it — nothing can toggle it now. The persisted flag is cleared on mount so anyone
  // who left the rail collapsed isn't stuck at 68px with no control to expand it.
  useEffect(() => { localStorage.removeItem('sanad.railCollapsed') }, [])

  const branding = useSyncExternalStore(subscribeBranding, getBranding)
  const [hiddenNav, setHiddenNav] = useState<string[]>([])

  // Org-wide navbar visibility (set by an admin in Settings → القائمة الجانبية).
  // Also load this user's personal table-column layout once the authed shell mounts.
  // Bumped once the admin-managed classification names are in, to repaint any badge that
  // rendered against the built-in labels while the request was in flight.
  const [labelsVersion, setLabelsVersion] = useState(0)

  useEffect(() => {
    navSettingsApi.get().then(setHiddenNav).catch(() => { })
    loadTablePrefs()
    loadCustomFieldCols()
    syncClassificationLabels().then((ok) => { if (ok) setLabelsVersion((v) => v + 1) })
  }, [])

  const isSysAdmin = useIsSystemAdmin()

  // Items, order, icons and permission gates all live in NAV_ITEMS so the sidebar and the
  // "hide sections" settings panel cannot drift apart. Only the label is resolved here.
  const ALL_NAV = NAV_ITEMS.map((n) => ({ ...n, label: t(n.labelKey), end: n.end ?? false }))

  // Sidebar is NOT filtered for System Admin — every module nav item shows, so the
  // platform operator can always reach every module. Org-level hiding still applies
  // to everyone: it is a tenant configuration choice, not a permission gate.
  const NAV = ALL_NAV.filter(
    (n) => (isSysAdmin || !n.perm || auth.hasPermission(n.perm)) && !hiddenNav.includes(n.key),
  )

  // Visible only to System Admin, and reached through routes guarded by a direct
  // role check (ProtectedRoute systemOnly) rather than the permission hook.
  //
  // TODO(spec §14.3): the section must carry all tenants, system-protected roles,
  // audit chain verification, encryption keys, and backup/restore. Those screens
  // do not exist yet (§29.5 lists the audit viewer as P1 Core, backup as P2, keys
  // as P3), so the list is empty and the section does not render. Populate it as
  // each screen lands — an entry here without a route renders a blank shell.
  const SYSTEM_NAV: { key: string; to: string; label: string; Icon: LucideIcon; end: boolean }[] =
    isSysAdmin ? [] : []

  const NAV_GROUPS = [
    { key: 'workspace', label: t('navgroup.workspace') },
    { key: 'records', label: t('navgroup.records') },
    { key: 'oversight', label: t('navgroup.oversight') },
    { key: 'system', label: t('navgroup.system') },
  ]

  // A section missing from the sidebar is inaccessible, not merely invisible: bounce direct
  // navigation (typing /app/archive) to the first section the user does have. Two reasons a
  // section is missing — the org hid it, or the user lacks its permission — and both bounce,
  // because otherwise the route renders the shell around a page whose every request 403s.
  // 'settings' is never hideable, so it is never bounced for that reason.
  useEffect(() => {
    const key = sectionKeyForPath(location.pathname)
    if (!key) return
    const item = ALL_NAV.find((n) => n.key === key)
    const lacksPermission = !isSysAdmin && !!item?.perm && !auth.hasPermission(item.perm)
    if ((key !== 'settings' && hiddenNav.includes(key)) || lacksPermission) {
      navigate(NAV[0]?.to ?? '/app', { replace: true })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname, hiddenNav, isSysAdmin])

  useEffect(() => { setMenuOpen(false); setCreateMenuOpen(false) }, [location.pathname])

  // Quick-create actions for the top-bar "+ إنشاء" menu (only those the user may create are shown).
  const CREATE_ACTIONS = [
    { label: t('create.document'), to: '/app/documents/new', perm: 'Documents.Create' },
    { label: t('create.external'), to: '/app/external', perm: 'Organization.Edit' },
  ].filter((a) => auth.hasPermission(a.perm))

  // Closing the desktop-program window ends the session, so it confirms first. Only while signed
  // in — the login screen has nothing to lose.
  useConfirmClose(auth.isAuthenticated())

  function logout() {
    // endSession, not auth.clear(): signing out here has to revoke the refresh token server-side
    // and close the session in any other tab, not only forget it in this one.
    endSession('manual')
    qc.clear()
    navigate('/login', { replace: true })
  }

  return (
    <div className={`shell ${menuOpen ? 'is-menu-open' : ''}`}>
      {/* Once, inside the authenticated shell — there is no session to time out before sign-in,
          and mounting it per page would restart the clock on every navigation. */}
      <IdleTimeout />
      {/* ── Sidebar rail: full height, navigation only — the brand sits in the header ── */}
      <aside className="shell__rail">
        <nav className="rail__nav">
          {NAV_GROUPS.map((g) => {
            const items = NAV.filter((n) => n.group === g.key)
            if (items.length === 0) return null
            return (
              <div className="rail__section" key={g.key}>
                <div className="rail__group">{g.label}</div>
                {items.map((n) => (
                  <NavLink
                    key={n.to}
                    to={n.to}
                    end={n.end}
                    onClick={() => setMenuOpen(false)}
                    title={n.label}
                    className={({ isActive }) => `rail__item ${isActive ? 'is-active' : ''}`}
                  >
                    <span className="rail__icon" aria-hidden><n.Icon size={17} strokeWidth={1.9} /></span>
                    <span className="rail__label">{n.label}</span>
                  </NavLink>
                ))}
              </div>
            )
          })}

          {/* System section — System Admin only (§14.3). Rendered outside the group
              loop because its visibility is a role check, not a permission filter. */}
          {SYSTEM_NAV.length > 0 && (
            <div className="rail__section" key="system-admin">
              <div className="rail__group">{t('nav.system')}</div>
              {SYSTEM_NAV.map((n) => (
                <NavLink
                  key={n.to}
                  to={n.to}
                  end={n.end}
                  onClick={() => setMenuOpen(false)}
                  title={n.label}
                  className={({ isActive }) => `rail__item ${isActive ? 'is-active' : ''}`}
                >
                  <span className="rail__icon" aria-hidden><n.Icon size={17} strokeWidth={1.9} /></span>
                  <span className="rail__label">{n.label}</span>
                </NavLink>
              ))}
            </div>
          )}
        </nav>

      </aside>

      {menuOpen && <div className="shell__backdrop shell__backdrop--drawer" onClick={() => setMenuOpen(false)} />}

      {/* ── Main wrap: header sits over the content only (prototype #mw) ── */}
      <div className="shell__mw">
        <header className="shell__navbar">
          {/* Brand — first in the header, so in RTL it anchors the leading edge.
              Links home: a masthead that does nothing is a dead click target. */}
          {/* Leading cluster — one grid column, so the search can centre against the
              header itself rather than against whatever this side happens to measure. */}
          <div className="shell__lead">
            <Link to="/app" className="nav__brand" aria-label={branding.nameAr || t('app.name')}>
              {branding.logoBase64
                ? <img src={branding.logoBase64} alt="" className="nav__logo" />
                : <BrandMark size={30} />}
              <span className="nav__brandtext">
                <span className="nav__brandname">{branding.nameAr || t('app.name')}</span>
                <span className="nav__brandsub">{t('app.name')}</span>
              </span>
            </Link>

            {/* Phone/tablet only. Removed from the desktop header by request; kept here
                because below 768px the rail is off-canvas and this is the only control
                that opens it — without it there is no way to reach navigation at all. */}
            <button
              className="shell__toggle"
              onClick={() => setMenuOpen((o) => !o)}
              aria-label={t('nav.menu')}
            >
              <Menu size={18} aria-hidden />
            </button>
          </div>

          <div className="shell__searchslot">
            {auth.hasPermission('Documents.View') && <GlobalDocumentSearch />}
          </div>

          <div className="shell__actions">
            <NotificationBell />
            {CREATE_ACTIONS.length > 0 && (
              <div className="shell__createwrap">
                <button className="shell__create" onClick={() => setCreateMenuOpen((v) => !v)} aria-label={t('create.menu')}>
                  <span className="shell__create-plus" aria-hidden>＋</span>
                  <span className="shell__create-label">{t('create.menu')}</span>
                </button>
                {createMenuOpen && (
                  <>
                    <div className="shell__createdrop">
                      {CREATE_ACTIONS.map((a) => (
                        <button key={a.to} className="shell__createdrop-btn"
                          onClick={() => { navigate(a.to); setCreateMenuOpen(false) }}>
                          {a.label}
                        </button>
                      ))}
                    </div>
                    <div className="shell__backdrop" onClick={() => setCreateMenuOpen(false)} />
                  </>
                )}
              </div>
            )}
            <InstallAppButton />
            <LanguageToggle />
            <ThemeToggle />
            {/* Account menu (picture · settings · sign out). Last in the cluster, so
                in RTL it sits on the far trailing edge of the header. */}
            <AccountMenu user={user} onLogout={logout} />
          </div>
        </header>

        <main className="shell__main">
          <div key={location.pathname} className="page-enter">
            {/* keyed on the label version so renamed classifications repaint immediately */}
            <Outlet key={labelsVersion} />
          </div>
        </main>
      </div>
    </div>
  )
}
