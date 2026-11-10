import { useEffect, useMemo, useState } from 'react'
import { createPortal } from 'react-dom'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { X } from 'lucide-react'
import { usersApi, type AdminUser, type UserNavSections } from '../lib/users'
import { NAV_SECTIONS } from '../lib/navSettings'
import { useToast } from './toast'
import '../styles/forms.css'
// .preview-overlay / .preview-modal — the app's own dialog shell.
import '../pages/documents/documents.css'

/**
 * Which sidebar sections one person sees.
 *
 * A head tidying an employee's rail down to the work they actually do. Unlike the permission
 * dialog beside it, this needs no ceiling: hiding is presentation, and a hidden section is still
 * reachable by URL if that person's permissions allow it. Anything that must actually be forbidden
 * is a permission, not a hidden section — the wording in the panel says so, because a head who
 * believes otherwise would use this as a lock.
 *
 * Sections already hidden institution-wide are listed as such and cannot be un-hidden here: that
 * decision belongs to the administrator, and offering a tick that does nothing would be a lie.
 */

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function UserSectionsDialog({ user, onClose }: {
  user: AdminUser
  onClose: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const [state, setState] = useState<UserNavSections | null>(null)
  const [hidden, setHidden] = useState<Set<string>>(new Set())
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let alive = true
    usersApi.getUserNavSections(user.id)
      .then((s) => { if (!alive) return; setState(s); setHidden(new Set(s.hidden)) })
      .catch((e) => toast.error(errOf(e, 'تعذّر تحميل الأقسام')))
    return () => { alive = false }
  }, [user.id, toast])

  const orgHidden = useMemo(() => new Set(state?.hiddenForEveryone ?? []), [state])

  /* The panel is phrased as "visible", not "hidden": a head reasons about what someone should
     see, and asking them to tick the negative was where the old org-wide screen confused people. */
  const toggleVisible = (key: string) =>
    setHidden((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key); else next.add(key)
      return next
    })

  const changed = useMemo(() => {
    const before = new Set(state?.hidden ?? [])
    return before.size !== hidden.size || [...hidden].some((k) => !before.has(k))
  }, [state, hidden])

  async function save() {
    setBusy(true)
    try {
      const saved = await usersApi.setUserNavSections(user.id, [...hidden])
      setState(saved)
      setHidden(new Set(saved.hidden))
      toast.success('حُدّثت أقسام المستخدم')
      onClose()
    } catch (e) { toast.error(errOf(e, 'تعذّر حفظ الأقسام')) }
    finally { setBusy(false) }
  }

  return createPortal(
    <div className="preview-overlay" onClick={() => !busy && onClose()}>
      <div className="preview-modal" onClick={(e) => e.stopPropagation()}
        role="dialog" aria-modal="true" aria-label="أقسام المستخدم"
        style={{ width: 'min(560px, 94vw)', height: 'auto', maxHeight: '90vh' }}>
        <header className="preview-head">
          <span className="preview-title">الأقسام الظاهرة · {user.fullName}</span>
          <button type="button" className="btn btn-ghost btn-sm" onClick={onClose}>
            <X size={15} aria-hidden /> إغلاق
          </button>
        </header>

        <div style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
          <p className="muted" style={{ marginTop: 0 }}>
            اختر ما يظهر لهذا المستخدم في القائمة الجانبية. هذا إخفاء للعرض فقط — ما يستطيع
            فتحه فعليًا تحدّده صلاحياته، فإن أردت المنع فاسحب الصلاحية.
          </p>

          {state === null ? <p className="muted">جارٍ التحميل…</p> : (
            <div className="dept-grant">
              {NAV_SECTIONS.map((s) => {
                const orgWide = orgHidden.has(s.key)
                return (
                  <label key={s.key} className="dept-grant__row"
                    title={orgWide ? 'مخفي على مستوى المؤسسة — يُعدَّل من إعدادات النظام' : undefined}>
                    <input
                      type="checkbox"
                      checked={!orgWide && !hidden.has(s.key)}
                      disabled={orgWide || busy}
                      onChange={() => toggleVisible(s.key)}
                    />
                    <span>
                      {t(s.labelKey)}
                      {orgWide && <span className="muted" style={{ fontSize: '.72rem' }}> — مخفي للجميع</span>}
                    </span>
                  </label>
                )
              })}
            </div>
          )}
        </div>

        <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
          <button type="button" className="btn btn-primary" disabled={busy || !changed || state === null}
            onClick={save}>{busy ? '…' : 'حفظ'}</button>
          <button type="button" className="btn btn-ghost" onClick={onClose}>إلغاء</button>
          <span className="muted" style={{ fontSize: '.75rem', marginInlineStart: 'auto' }}>
            تظهر التغييرات للمستخدم عند تحديث صفحته.
          </span>
        </div>
      </div>
    </div>,
    document.body,
  )
}
