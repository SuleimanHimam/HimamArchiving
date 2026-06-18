import { useState, useEffect } from 'react'
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom'
import { auth } from '../lib/auth'
import NotificationBell from './NotificationBell'
import './applayout.css'

const NAV = [
  { to: '/app', label: 'الرئيسية', icon: '◈', end: true, perm: null },
  { to: '/app/incoming', label: 'الوارد', icon: '↙', end: false, perm: 'IncomingMail.View' },
  { to: '/app/outgoing', label: 'الصادر', icon: '↗', end: false, perm: 'OutgoingMail.View' },
  { to: '/app/documents', label: 'الوثائق', icon: '▤', end: false, perm: 'Documents.View' },
  { to: '/app/workflow', label: 'سير العمل', icon: '⇄', end: false, perm: 'Workflow.View' },
  { to: '/app/archive', label: 'الأرشيف', icon: '▦', end: false, perm: 'Archive.View' },
  { to: '/app/reports', label: 'التقارير', icon: '◷', end: false, perm: 'Reports.View' },
  { to: '/app/settings', label: 'الإعدادات', icon: '⚙', end: false, perm: 'Settings.View' },
]

export default function AppLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const user = auth.getUser()
  const [menuOpen, setMenuOpen] = useState(false)

  // Close the mobile drawer whenever the route changes.
  useEffect(() => { setMenuOpen(false) }, [location.pathname])

  function logout() {
    auth.clear()
    navigate('/login', { replace: true })
  }

  return (
    <div className={`shell ${menuOpen ? 'is-menu-open' : ''}`}>
      {/* Mobile top bar */}
      <header className="shell__topbar">
        <button className="shell__burger" onClick={() => setMenuOpen((o) => !o)} aria-label="القائمة">
          {menuOpen ? '✕' : '☰'}
        </button>
        <span className="shell__topbrand">الديوان الإلكتروني</span>
        <NotificationBell />
      </header>

      {/* Drawer backdrop (mobile) */}
      <div className="shell__backdrop" onClick={() => setMenuOpen(false)} />

      <aside className="shell__side">
        <div className="shell__brand">
          <div className="stamp shell__seal"><span>الديوان</span></div>
          <span className="kicker">الديوان الإلكتروني</span>
        </div>

        <div className="shell__user">
          <div className="shell__avatar">{user?.fullName?.[0] ?? '؟'}</div>
          <div className="shell__userinfo">
            <div className="shell__username">{user?.fullName}</div>
            <div className="shell__userrole mono">{user?.roles?.[0]}</div>
          </div>
          <span className="shell__userbell"><NotificationBell /></span>
        </div>

        <nav className="shell__nav">
          {NAV.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              onClick={() => setMenuOpen(false)}
              className={({ isActive }) => `shell__navitem ${isActive ? 'is-active' : ''}`}
            >
              <span className="shell__navicon">{n.icon}</span>
              <span>{n.label}</span>
            </NavLink>
          ))}
        </nav>

        <button className="shell__logout" onClick={logout}>تسجيل الخروج</button>
      </aside>

      <main className="shell__main">
        <Outlet />
      </main>
    </div>
  )
}
