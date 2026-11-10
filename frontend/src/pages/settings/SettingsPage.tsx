import { Navigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import AboutSettings from '../../components/AboutSettings'
import ClassificationSettings from '../../components/ClassificationSettings'
import TableColumnsSettings from '../../components/TableColumnsSettings'
import CustomFieldsSettings from '../../components/CustomFieldsSettings'
import MailTypesSettings from '../../components/MailTypesSettings'
import DestructionMethodsSettings from '../../components/DestructionMethodsSettings'
import NavigationSettings from '../../components/NavigationSettings'
import BrandingSettings from '../../components/BrandingSettings'
import GroupsSettings from '../../components/GroupsSettings'
import LicenseSettings from '../../components/LicenseSettings'
import SettingsNav from '../../components/SettingsNav'
import { useSettingsSections } from './sections'
import '../../styles/forms.css'
import './settings.css'

/** The panel each section opens, keyed as in `sections.ts`. */
const PANELS: Record<string, () => React.ReactNode> = {
  branding: () => <BrandingSettings />,
  groups: () => <GroupsSettings />,
  navbar: () => <NavigationSettings />,
  classification: () => <ClassificationSettings />,
  tableColumns: () => <TableColumnsSettings />,
  customFields: () => <CustomFieldsSettings />,
  mailTypes: () => <MailTypesSettings />,
  destructionMethods: () => <DestructionMethodsSettings />,
  license: () => <LicenseSettings />,
  about: () => <AboutSettings />,
}

export default function SettingsPage() {
  const { t } = useTranslation()
  const sections = useSettingsSections()

  const [searchParams, setSearchParams] = useSearchParams()
  const panels = sections.filter((s) => PANELS[s.key])

  /* The URL is the only source of truth for which section is open — not component state beside
     it. Held in state, the browser's Back button changed the address while the panel stayed put:
     you were on ?tab=printDesign looking at أعمدة الجداول. */
  const tabFromUrl = searchParams.get('tab')
  // الماسحات والطابعات is its own section now. An old ?tab=scanner link would otherwise fall
  // through to the first panel and silently show هوية المؤسسة instead.
  if (tabFromUrl === 'scanner') return <Navigate to="/app/scanner" replace />
  // Same for تصاميم الطباعة, carrying any design id so a bookmarked design still opens on it.
  if (tabFromUrl === 'printDesign') {
    const rest = new URLSearchParams(searchParams); rest.delete('tab')
    return <Navigate to={`/app/print-design${rest.size ? `?${rest}` : ''}`} replace />
  }
  const active = panels.some((s) => s.key === tabFromUrl) ? tabFromUrl! : (panels[0]?.key ?? 'branding')
  const current = panels.find((s) => s.key === active) ?? panels[0]

  /* Switching drops every other parameter: those belong to the section being left (a design id,
     say), and carrying them into the next one would reopen a designer inside the wrong panel.
     `replace` keeps Back as "leave Settings" rather than a walk back through every tab visited. */
  function select(key: string) {
    setSearchParams({ tab: key }, { replace: true })
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <h1>{t('settings.title')}</h1>
        </div>
      </header>

      <div className="settings-layout">
        <SettingsNav active={active} onSelect={select} />
        <div className="settings-panel">
          {current ? PANELS[current.key]?.() : null}
        </div>
      </div>
    </div>
  )
}
