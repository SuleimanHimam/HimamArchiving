import { useEffect, useState } from 'react'
import { motion } from 'motion/react'
import { BadgeCheck, Copy, Check, ShieldAlert, Loader2 } from 'lucide-react'
import axios from 'axios'
import { licenseApi, LICENSE_STATE_LABELS, type LicenseStatus } from '../lib/license'
import { auth } from '../lib/auth'
import { useToast } from './toast'
import { useTranslation } from 'react-i18next'
import { formatDate } from '../lib/datetime'
import '../pages/settings/settings.css'

/**
 * Licence panel: shows what is installed, shows this server's fingerprint so it can be sent to us,
 * and takes a new key. The fingerprint is the part that matters operationally — a licence is signed
 * against it, so a key issued for one server does nothing on another.
 */
export default function LicenseSettings() {
  const { i18n } = useTranslation()
  const { success, error } = useToast()
  const canEdit = auth.hasPermission('Settings.Edit')

  const [status, setStatus] = useState<LicenseStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [key, setKey] = useState('')
  const [saving, setSaving] = useState(false)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    licenseApi.status()
      .then(setStatus)
      .catch(() => error('تعذّر قراءة حالة الترخيص'))
      .finally(() => setLoading(false))
  }, [error])

  async function copyMachineId() {
    if (!status) return
    await navigator.clipboard.writeText(status.machineId)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  async function install() {
    if (!key.trim()) return
    setSaving(true)
    try {
      const next = await licenseApi.install(key.trim())
      setStatus(next)
      setKey('')
      success('تم تفعيل الترخيص')
    } catch (e) {
      // A refused key comes back as the evaluated status, so the reason can be shown verbatim
      // rather than as a generic failure.
      const rejected = axios.isAxiosError(e) ? (e.response?.data as LicenseStatus | undefined) : undefined
      error(rejected?.message ?? 'تعذّر تفعيل الترخيص')
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center gap-2 p-6 text-sm text-muted-foreground">
        <Loader2 className="animate-spin" size={16} /> جارٍ التحميل…
      </div>
    )
  }
  if (!status) return null

  const ok = status.isUsable
  const warn = status.state === 'Grace'
  const tone = warn
    ? 'border-amber-400/40 bg-amber-50 text-amber-800'
    : ok
      ? 'border-emerald-500/30 bg-emerald-50 text-emerald-800'
      : 'border-red-500/30 bg-red-50 text-red-800'

  const rows: [string, React.ReactNode][] = [
    ['الجهة المرخَّصة', status.customer ?? '—'],
    ['الإصدار', status.edition ?? '—'],
    ['رقم الترخيص', <span className="font-mono" dir="ltr">{status.licenseId ?? '—'}</span>],
    ['تاريخ الإصدار', status.issuedAt ? formatDate(status.issuedAt, i18n.language) : '—'],
    ['تاريخ الانتهاء', status.expiresAt
      ? `${formatDate(status.expiresAt, i18n.language)}${status.daysRemaining !== null ? ` (${status.daysRemaining} يوم)` : ''}`
      : 'دائم'],
    ['الحد الأقصى للمستخدمين', status.maxUsers ?? 'غير محدود'],
    ['الوحدات المفعّلة', status.features.length ? status.features.join('، ') : 'جميع الوحدات'],
  ]

  return (
    <motion.section
      className="rounded-doc-lg border border-parchment-3 bg-paper p-5 shadow-sm"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
    >
      <h3 className="mb-4 flex items-center gap-2 font-display text-lg text-ink">
        {ok ? <BadgeCheck size={18} /> : <ShieldAlert size={18} />}
        الترخيص
      </h3>

      <div className={`mb-5 rounded-doc border px-4 py-3 text-sm ${tone}`}>
        <div className="font-semibold">{LICENSE_STATE_LABELS[status.state]}</div>
        <div className="mt-0.5 opacity-90" dir="auto">{status.message}</div>
      </div>

      {/* The fingerprint is shown whether or not a licence is installed: an unlicensed server is
          exactly when someone needs to read it off the screen and send it to us. */}
      <div className="mb-5">
        <div className="mb-1.5 text-sm text-muted-foreground">معرّف هذا الخادم (يُرسل عند طلب الترخيص)</div>
        <div className="flex items-center gap-2">
          <code className="flex-1 rounded-doc border border-parchment-3 bg-parchment-1 px-3 py-2 font-mono text-sm tracking-wider" dir="ltr">
            {status.machineId}
          </code>
          <button type="button" className="btn btn-ghost btn-sm" onClick={copyMachineId} title="نسخ">
            {copied ? <Check size={16} /> : <Copy size={16} />}
          </button>
        </div>
      </div>

      <dl className="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="text-ink-text">{value}</dd>
          </div>
        ))}
      </dl>

      {canEdit && (
        <div className="mt-6 border-t border-parchment-3 pt-5">
          <label htmlFor="license-key" className="mb-1.5 block text-sm text-muted-foreground">
            تفعيل ترخيص جديد — ألصق مفتاح الترخيص هنا
          </label>
          <textarea
            id="license-key"
            className="w-full rounded-doc border border-parchment-3 bg-paper p-3 font-mono text-xs"
            dir="ltr"
            rows={5}
            spellCheck={false}
            placeholder="ARCHLIC-1...."
            value={key}
            onChange={(e) => setKey(e.target.value)}
          />
          <div className="mt-3 flex items-center gap-3">
            <button type="button" className="btn btn-primary" onClick={install} disabled={saving || !key.trim()}>
              {saving ? <Loader2 className="animate-spin" size={16} /> : null}
              تفعيل
            </button>
            <span className="text-xs text-muted-foreground">
              يُقبل المفتاح فقط إذا كان صادرًا لهذا الخادم بالذات.
            </span>
          </div>
        </div>
      )}
    </motion.section>
  )
}
