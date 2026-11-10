import { useEffect, useMemo, useState } from 'react'
import { Sparkles } from 'lucide-react'
import { auth } from '../lib/auth'
import { account } from '../lib/account'
import { reportTemplates, renderLayout, SCOPE_LABEL, type ReportTemplate } from '../lib/reportTemplates'
import { useToast } from './toast'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './ui/dialog'
import './design-picker.css'

/**
 * "Which letterhead do my outgoing letters print on?"
 *
 * Designs resolve positionally (User → department → institution), which answers what *applies*
 * to someone in your seat but never lets a person choose between the ones open to them. This is
 * that choice, and it is saved on the user — so it follows them to any machine, and a colleague
 * printing their letter still gets their letterhead.
 *
 * Only designs the user could already print with are offered; the server enforces the same rule
 * again on save, because a dialog is not a permission check.
 */
export default function DesignPickerDialog({ open, onClose, target = 1 }: {
  open: boolean
  onClose: () => void
  /** Which register's designs to offer. 0 = البريد الداخلي, 1 = الصادر الخارجي. */
  target?: 0 | 1
}) {
  const toast = useToast()
  const me = auth.getUser()
  const [all, setAll] = useState<ReportTemplate[] | null>(null)
  const [saving, setSaving] = useState(false)
  // Each register keeps its own choice, so picking an internal design never unsets the external one.
  const [chosen, setChosen] = useState<number | null>(
    (target === 0 ? me?.defaultMailTemplateId : me?.defaultPrintTemplateId) ?? null)

  // Radix unmounts the content when the dialog closes, so `chosen` starts fresh on every open —
  // no effect is needed to re-sync it.
  useEffect(() => {
    if (!open) return
    reportTemplates.list().then(setAll).catch(() => setAll([]))
  }, [open])

  const TARGET_NAME = target === 1 ? 'Document' : 'Correspondence'

  const mine = useMemo(() => (all ?? []).filter((t) => {
    if (!t.isActive) return false
    if (t.target !== TARGET_NAME && t.target !== 'All') return false
    if (t.scope === 'Default') return true
    if (t.scope === 'OrgUnit') return t.orgUnitId != null && t.orgUnitId === me?.orgUnitId
    return t.userId === me?.id
  }), [all, TARGET_NAME, me?.orgUnitId, me?.id])

  async function pick(templateId: number | null) {
    setSaving(true)
    try {
      await account.setDefaultTemplate(templateId, target)
      setChosen(templateId)
      toast.success(templateId == null ? 'أُعيد التصميم التلقائي' : 'صار هذا تصميمك الافتراضي')
      onClose()
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      toast.error(msg ?? 'تعذّر حفظ التصميم')
    } finally { setSaving(false) }
  }

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="dsn">
        <DialogHeader>
          <DialogTitle>{target === 0 ? 'تصميم رسائلي الداخلية' : 'تصميم كتبي الصادرة'}</DialogTitle>
        </DialogHeader>
        <p className="muted dsn__lede">
          هذه التصاميم المتاحة لك: تصاميمك الشخصية، وتصاميم قسمك، وتصاميم المؤسسة العامة.
          ما تختاره هنا يُطبَّق على {target === 0 ? 'رسائلك الداخلية' : 'كتبك الصادرة'} عند الطباعة
          والمعاينة — وليس على هذه الرسالة وحدها.
        </p>

        <div className="dsn__grid">
          {/* Clearing the choice is a card of its own: "no design" is a real, reachable state. */}
          <button className={`dsn__card dsn__card--auto ${chosen == null ? 'is-on' : ''}`}
            disabled={saving} onClick={() => pick(null)}>
            <span className="dsn__autoic" aria-hidden><Sparkles size={18} /></span>
            <strong>تلقائي</strong>
            <span className="muted">تصميمك، فتصميم قسمك، فتصميم المؤسسة</span>
          </button>

          {all === null && <p className="muted">جارٍ التحميل…</p>}
          {all !== null && mine.length === 0 && <p className="muted">لا توجد تصاميم متاحة لك بعد.</p>}

          {mine.map((t) => (
            <button key={t.id} className={`dsn__card ${chosen === t.id ? 'is-on' : ''}`}
              disabled={saving} onClick={() => pick(t.id)} title={t.name}>
              <Thumb template={t} />
              <strong className="dsn__name">{t.name}</strong>
              <span className="muted dsn__scope">
                {SCOPE_LABEL[t.scope] ?? t.scope}
                {t.orgUnitName ? ` · ${t.orgUnitName}` : ''}
              </span>
              {chosen === t.id && <span className="dsn__badge">المستخدَم الآن</span>}
            </button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  )
}

/** The design itself, scaled down — the same renderer the print path uses. */
function Thumb({ template }: { template: ReportTemplate }) {
  const html = useMemo(() => {
    try {
      return renderLayout(template.layoutJson, {
        tokens: {
          institutionName: 'مؤسسة همم', number: 'OUT-2026-0001', subject: 'نموذج',
          date: '2026-08-02', recipient: 'الجهة المستلمة', sender: 'الجهة المرسِلة',
        },
        logo: template.logoBase64,
      })
    } catch { return null }
  }, [template.layoutJson, template.logoBase64])

  if (!html) return <span className="dsn__thumb dsn__thumb--bad">تعذّر العرض</span>
  return (
    <span className="dsn__thumb">
      <span className="dsn__page" aria-hidden dangerouslySetInnerHTML={{ __html: html }} />
    </span>
  )
}
