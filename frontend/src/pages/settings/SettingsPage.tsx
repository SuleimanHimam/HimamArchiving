import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import ScannerSettings from '../../components/ScannerSettings'
import UsersAdmin from '../../components/UsersAdmin'
import AboutSettings from '../../components/AboutSettings'
import DesignatedCommunitySettings from '../../components/DesignatedCommunitySettings'
import PreservationPolicySettings from '../../components/PreservationPolicySettings'
import BackupSettings from '../../components/BackupSettings'
import RolesAdmin from '../../components/RolesAdmin'
import ClassificationSettings from '../../components/ClassificationSettings'
import BrandingSettings from '../../components/BrandingSettings'
import '../incoming/incoming.css'
import './settings.css'

interface Section { key: string; label: string; icon: string; show: boolean; render: () => React.ReactNode }

export default function SettingsPage() {
  const { t } = useTranslation()

  const sections: Section[] = [
    { key: 'branding', label: 'هوية المؤسسة', icon: '◉', show: auth.hasPermission('Organization.View'), render: () => <BrandingSettings /> },
    { key: 'scanner', label: t('settings.sections.scanner'), icon: '⎙', show: auth.hasPermission('Scanner.View'), render: () => <ScannerSettings /> },
    { key: 'users', label: t('settings.sections.users'), icon: '◔', show: auth.hasPermission('Users.View'), render: () => <UsersAdmin /> },
    { key: 'roles', label: t('settings.sections.roles'), icon: '⊙', show: auth.hasPermission('Users.View'), render: () => <RolesAdmin /> },
    { key: 'classification', label: t('settings.sections.classification'), icon: '◈', show: auth.hasPermission('Classification.View'), render: () => <ClassificationSettings /> },
    { key: 'preservation', label: t('settings.sections.preservation'), icon: '⛁', show: auth.hasPermission('Preservation.View'), render: () => (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '1.2rem' }}>
        <PreservationPolicySettings />
        <DesignatedCommunitySettings />
      </div>
    ) },
    { key: 'backup', label: t('settings.sections.backup'), icon: '⛃', show: auth.hasPermission('Backup.Edit'), render: () => <BackupSettings /> },
    { key: 'about', label: t('settings.sections.about'), icon: 'ℹ', show: true, render: () => <AboutSettings /> },
  ].filter((s) => s.show)

  const [searchParams] = useSearchParams()
  const tabFromUrl = searchParams.get('tab')
  const [active, setActive] = useState(
    sections.some((s) => s.key === tabFromUrl) ? tabFromUrl! : (sections[0]?.key ?? 'scanner'),
  )
  const current = sections.find((s) => s.key === active) ?? sections[0]

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('settings.kicker')}</span>
          <h1>{t('settings.title')}</h1>
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
