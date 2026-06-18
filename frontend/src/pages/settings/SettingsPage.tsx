import { useState } from 'react'
import { auth } from '../../lib/auth'
import ScannerSettings from '../../components/ScannerSettings'
import UsersAdmin from '../../components/UsersAdmin'
import AboutSettings from '../../components/AboutSettings'
import DesignatedCommunitySettings from '../../components/DesignatedCommunitySettings'
import PreservationPolicySettings from '../../components/PreservationPolicySettings'
import '../incoming/incoming.css'
import './settings.css'

interface Section { key: string; label: string; icon: string; show: boolean; render: () => React.ReactNode }

export default function SettingsPage() {
  const sections: Section[] = [
    { key: 'scanner', label: 'الماسحات الضوئية', icon: '⎙', show: true, render: () => <ScannerSettings /> },
    { key: 'users', label: 'المستخدمون والصلاحيات', icon: '◔', show: auth.hasPermission('Users.View'), render: () => <UsersAdmin /> },
    { key: 'preservation', label: 'الحفظ الرقمي', icon: '⛁', show: auth.hasPermission('Settings.View'), render: () => (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
        <PreservationPolicySettings />
        <DesignatedCommunitySettings />
      </div>
    ) },
    { key: 'about', label: 'حول النظام', icon: 'ℹ', show: true, render: () => <AboutSettings /> },
  ].filter((s) => s.show)

  const [active, setActive] = useState(sections[0]?.key ?? 'scanner')
  const current = sections.find((s) => s.key === active) ?? sections[0]

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">SETTINGS · الإعدادات</span>
          <h1>الإعدادات</h1>
        </div>
      </header>

      <div className="settings-layout">
        <nav className="settings-tabs">
          {sections.map((s) => (
            <button
              key={s.key}
              className={`settings-tab ${active === s.key ? 'is-active' : ''}`}
              onClick={() => setActive(s.key)}
            >
              <span className="settings-tab__icon" aria-hidden>{s.icon}</span>
              <span>{s.label}</span>
            </button>
          ))}
        </nav>

        <div className="settings-panel">
          {current?.render()}
        </div>
      </div>
    </div>
  )
}
