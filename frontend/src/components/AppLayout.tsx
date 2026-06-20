import { useState, useEffect, useSyncExternalStore } from 'react'
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { auth } from '../lib/auth'
import { useCurrentUser } from '../lib/useCurrentUser'
import { getBranding, subscribeBranding } from '../lib/branding'
import { navSettingsApi } from '../lib/navSettings'
import NotificationBell from './NotificationBell'
import LanguageToggle from './LanguageToggle'
import GlobalDocumentSearch from './GlobalDocumentSearch'
import './applayout.css'

// Maps a path under /app to its navbar section key (or null for unknown paths).
const SECTION_KEYS = ['incoming', 'outgoing', 'documents', 'notes', 'workflow', 'archive', 'reports', 'monitoring', 'settings']
function sectionKeyForPath(pathname: string): string | null {
  const m = pathname.match(/^\/app(?:\/([^/]+))?/)
  if (!m) return null
  const seg = m[1]
  if (!seg) return 'home'
  return SECTION_KEYS.includes(seg) ? seg : null
}

export default function AppLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const qc = useQueryClient()
  const { data: user } = useCurrentUser()
  const [menuOpen, setMenuOpen] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)

  const branding = useSyncExternalStore(subscribeBranding, getBranding)
  const [hiddenNav, setHiddenNav] = useState<string[]>([])

  // Org-wide navbar visibility (set by an admin in Settings → القائمة الجانبية).
  useEffect(() => { navSettingsApi.get().then(setHiddenNav).catch(() => {}) }, [])

  const NAV = [
    { key: 'home', to: '/app', label: t('nav.home'), icon: '◈', end: true, perm: null },
    { key: 'incoming', to: '/app/incoming', label: t('nav.incoming'), icon: '↙', end: false, perm: 'IncomingMail.View' },
    { key: 'outgoing', to: '/app/outgoing', label: t('nav.outgoing'), icon: '↗', end: false, perm: 'OutgoingMail.View' },
    { key: 'documents', to: '/app/documents', label: t('nav.documents'), icon: '▤', end: false, perm: 'Documents.View' },
    { key: 'notes', to: '/app/notes', label: t('nav.notes'), icon: '✎', end: false, perm: null },
    { key: 'workflow', to: '/app/workflow', label: t('nav.workflow'), icon: '⇄', end: false, perm: 'Workflow.View' },
    { key: 'archive', to: '/app/archive', label: t('nav.archive'), icon: '▦', end: false, perm: 'Archive.View' },
    { key: 'reports', to: '/app/reports', label: t('nav.reports'), icon: '◷', end: false, perm: 'Reports.View' },
    { key: 'monitoring', to: '/app/monitoring', label: t('nav.monitoring'), icon: '⊡', end: false, perm: 'Audit.View' },
    { key: 'settings', to: '/app/settings', label: t('nav.settings'), icon: '⚙', end: false, perm: 'Scanner.View' },
  ].filter((n) => (!n.perm || auth.hasPermission(n.perm)) && !hiddenNav.includes(n.key))

  // Hidden sections are inaccessible, not just invisible: bounce direct navigation
  // (e.g. typing /app/reports) to the first available section. 'settings' is never hideable.
  useEffect(() => {
    const key = sectionKeyForPath(location.pathname)
    if (key && key !== 'settings' && hiddenNav.includes(key)) {
      navigate(NAV[0]?.to ?? '/app', { replace: true })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname, hiddenNav])

  useEffect(() => { setMenuOpen(false); setUserMenuOpen(false) }, [location.pathname])

  function logout() {
    auth.clear()
    qc.clear()
    navigate('/login', { replace: true })
  }

  return (
    <div className={`shell ${menuOpen ? 'is-menu-open' : ''}`}>
      {/* ── Top Navbar ── */}
      <header className="shell__navbar">
        {/* Brand */}
        <div className="shell__brand">
          {branding.logoBase64
            ? <img src={branding.logoBase64} alt="logo" className="shell__logo" />
            : <span className="shell__seal-icon">◈</span>}
          <span className="shell__brandname">
            {branding.nameAr || t('app.name')}
          </span>
        </div>

        {/* Nav links — desktop */}
        <nav className="shell__nav">
          {NAV.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              className={({ isActive }) => `shell__navitem ${isActive ? 'is-active' : ''}`}
            >
              <span className="shell__navicon">{n.icon}</span>
              <span className="shell__navlabel">{n.label}</span>
            </NavLink>
          ))}
        </nav>

        {/* Global document search */}
        {auth.hasPermission('Documents.View') && <GlobalDocumentSearch />}

        {/* Right actions */}
        <div className="shell__actions">
          <LanguageToggle />
          <NotificationBell />

          {/* User chip */}
          <div className="shell__user" onClick={() => setUserMenuOpen((v) => !v)}>
            <div className="shell__avatar">{user?.fullName?.[0] ?? '؟'}</div>
            <span className="shell__username">{user?.fullName}</span>
            <span className="shell__caret">▾</span>
          </div>

          {/* User dropdown */}
          {userMenuOpen && (
            <>
              <div className="shell__userdrop">
                <div className="shell__userdrop-name">{user?.fullName}</div>
                <div className="shell__userdrop-role mono">{user?.roles?.[0]}</div>
                <hr className="shell__userdrop-sep" />
                <button className="shell__userdrop-btn shell__logout" onClick={logout}>
                  {t('nav.logout')}
                </button>
              </div>
              <div className="shell__backdrop" onClick={() => setUserMenuOpen(false)} />
            </>
          )}
        </div>

        {/* Burger — mobile only */}
        <button className="shell__burger" onClick={() => setMenuOpen((o) => !o)} aria-label={t('nav.menu')}>
          {menuOpen ? '✕' : '☰'}
        </button>
      </header>

      {/* Mobile drawer */}
      <nav className="shell__drawer">
        {NAV.map((n) => (
          <NavLink
            key={n.to}
            to={n.to}
            end={n.end}
            onClick={() => setMenuOpen(false)}
            className={({ isActive }) => `shell__drawer-item ${isActive ? 'is-active' : ''}`}
          >
            <span className="shell__navicon">{n.icon}</span>
            <span>{n.label}</span>
          </NavLink>
        ))}
        <hr className="shell__userdrop-sep" style={{ margin: '.5rem 0', borderColor: 'rgba(255,255,255,.1)' }} />
        <button className="shell__logout" onClick={logout}>{t('nav.logout')}</button>
      </nav>

      {menuOpen && <div className="shell__backdrop shell__backdrop--drawer" onClick={() => setMenuOpen(false)} />}

      <main className="shell__main">
        <Outlet />
      </main>
    </div>
  )
}
