import { useEffect, useState } from 'react'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { navSettingsApi, NAV_SECTIONS } from '../lib/navSettings'
import { useToast } from './toast'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

export default function NavigationSettings() {
  const { t } = useTranslation()
  const toast = useToast()
  const [hidden, setHidden] = useState<string[]>([])
  const [busy, setBusy] = useState(false)

  useEffect(() => { navSettingsApi.get().then(setHidden).catch(() => {}) }, [])

  const toggle = (key: string) =>
    setHidden((h) => (h.includes(key) ? h.filter((k) => k !== key) : [...h, key]))

  async function save() {
    setBusy(true)
    try {
      setHidden(await navSettingsApi.update(hidden))
      toast.success('تم حفظ إعدادات القائمة — قد يحتاج المستخدمون لإعادة تحميل الصفحة')
    } catch { toast.error('تعذّر حفظ الإعدادات') }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">القائمة الجانبية</h3>
      <p className="muted">
        اختر الأقسام الظاهرة في القائمة لجميع المستخدمين. الأقسام غير المحددة ستُخفى عن الجميع
        (يبقى قسم «الإعدادات» ظاهرًا دائمًا، كما تبقى صلاحيات كل مستخدم سارية).
      </p>

      <ul className="nav-toggle-list">
        {NAV_SECTIONS.map((s) => {
          const visible = !hidden.includes(s.key)
          return (
            <li key={s.key}>
              <label className="nav-toggle">
                <input type="checkbox" checked={visible} onChange={() => toggle(s.key)} />
                <span>{t(s.labelKey)}</span>
                <span className={`badge ${visible ? 'internal' : ''}`}>{visible ? 'ظاهر' : 'مخفي'}</span>
              </label>
            </li>
          )
        })}
      </ul>

      <div className="form-actions">
        <button className="btn btn-primary" disabled={busy} onClick={save}>{busy ? '…جارٍ الحفظ' : 'حفظ'}</button>
      </div>
    </motion.section>
  )
}
