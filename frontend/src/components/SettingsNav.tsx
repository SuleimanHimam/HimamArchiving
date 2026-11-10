import { useSettingsSections } from '../pages/settings/sections'
import '../pages/settings/settings.css'

/**
 * The list of Settings sections. Every entry switches the panel in place — no entry navigates,
 * so no click behaves differently from its neighbour.
 */
export default function SettingsNav({
  active, onSelect,
}: {
  /** Key of the section currently shown. */
  active?: string
  onSelect: (key: string) => void
}) {
  const sections = useSettingsSections()

  return (
    <nav className="settings-tabs" aria-label="أقسام الإعدادات">
      {sections.map((s) => (
        <button
          key={s.key}
          type="button"
          className={`settings-tab ${s.key === active ? 'is-active' : ''}`}
          onClick={() => onSelect(s.key)}
          aria-current={s.key === active ? 'page' : undefined}
        >
          <span className="settings-tab__icon" aria-hidden><s.Icon size={16} /></span>
          <span>{s.label}</span>
        </button>
      ))}
    </nav>
  )
}
