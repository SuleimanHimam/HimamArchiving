import { useState } from 'react'
import { RotateCcw } from 'lucide-react'
import {
  getAppearance, applyAppearance, resetAppearance,
  APPEARANCE_DEFAULTS, SIDEBAR_FONTS, type Appearance,
} from '../lib/appearance'
import { useToast } from './toast'
import { LabeledSelect } from './ui/labeled-input'
import './brandingsettings.css'

export default function AppearanceSettings() {
  const toast = useToast()
  const [form, setForm] = useState<Appearance>(getAppearance)

  // Apply (and persist) immediately so the sidebar/system update live as you pick.
  function set(patch: Partial<Appearance>) {
    const next = { ...form, ...patch }
    applyAppearance(next)
    setForm(next)
  }

  function reset() {
    resetAppearance()
    setForm(getAppearance())
    toast.success('تمت إعادة المظهر الافتراضي')
  }

  const colorField = (
    label: string,
    key: keyof Appearance,
    fallback: string,
  ) => (
    <div className="branding-color-item">
      <label className="branding-label">{label}</label>
      <div className="branding-color-row">
        <input
          type="color"
          className="branding-color-picker"
          value={(form[key] as string | null) ?? fallback}
          onChange={(e) => set({ [key]: e.target.value } as Partial<Appearance>)}
        />
        <span className="branding-color-hex mono">{(form[key] as string | null) ?? fallback}</span>
        <div className="branding-color-swatch" style={{ background: (form[key] as string | null) ?? fallback }} />
        {form[key] && (
          <button className="btn btn-ghost btn-sm" onClick={() => set({ [key]: null } as Partial<Appearance>)}><RotateCcw size={14} aria-hidden /></button>
        )}
      </div>
    </div>
  )

  return (
    // Heading and description come from the enclosing section in BrandingSettings, so this
    // renders the controls only — two titles read as two separate settings.
    <div className="branding-wrap">
      {/* Sidebar colors */}
      <div className="branding-section">
        <div className="branding-section-title">ألوان الشريط الجانبي</div>
        <div className="branding-colors">
          {colorField('خلفية الشريط الجانبي', 'railBg', APPEARANCE_DEFAULTS.railBg)}
          {colorField('لون نص الشريط الجانبي', 'railText', APPEARANCE_DEFAULTS.railText)}
          {colorField('لون التمييز (الأيقونات والشريط النشط)', 'railAccent', APPEARANCE_DEFAULTS.railAccent)}
        </div>
      </div>

      {/* Sidebar font */}
      <div className="branding-section">
        <div className="branding-section-title">خط الشريط الجانبي</div>
        <div className="branding-field" style={{ maxWidth: 360 }}>
          <LabeledSelect label="نوع الخط" value={form.railFont ?? ''}
            onChange={(e) => set({ railFont: e.target.value || null })}>
            {SIDEBAR_FONTS.map((f) => <option key={f.label} value={f.value}>{f.label}</option>)}
          </LabeledSelect>
          <div
            style={{ marginTop: 8, padding: '8px 12px', borderRadius: 8, background: form.railBg ?? APPEARANCE_DEFAULTS.railBg,
              color: form.railText ?? APPEARANCE_DEFAULTS.railText, fontFamily: form.railFont || 'var(--font-body)' }}
          >
            معاينة · الرئيسية · المراسلات · الوثائق
          </div>
        </div>
      </div>

      {/* System background + foreground */}
      <div className="branding-section">
        <div className="branding-section-title">خلفية وألوان النظام</div>
        <div className="branding-colors">
          {colorField('خلفية النظام', 'appBg', APPEARANCE_DEFAULTS.appBg)}
          {colorField('لون النص العام', 'appText', APPEARANCE_DEFAULTS.appText)}
        </div>
        <p className="text-muted-foreground" style={{ fontSize: 12, marginTop: 6 }}>
          تُطبَّق خلفية وألوان النظام على الوضعين الفاتح والداكن معًا.
        </p>
      </div>

      <div className="branding-actions">
        <button className="btn btn-ghost" onClick={reset}><RotateCcw size={15} aria-hidden /> إعادة المظهر الافتراضي</button>
      </div>
    </div>
  )
}
