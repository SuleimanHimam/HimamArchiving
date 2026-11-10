import { useEffect, useMemo, useState } from 'react'
import { createPortal } from 'react-dom'
import { AxiosError } from 'axios'
import { Lock, X } from 'lucide-react'
import {
  usersApi, type AdminUser, type PermissionInfo, type UserPermissions,
  RESOURCE_LABELS, ACTION_LABELS, ACTION_ORDER, RESOURCE_GROUPS,
} from '../lib/users'
import { useToast } from './toast'
import '../styles/forms.css'
// .preview-overlay / .preview-modal — the app's own dialog shell.
import '../pages/documents/documents.css'

/**
 * Permissions for one person, on top of what their roles carry.
 *
 * A department head cannot edit roles — those are the institution's definition of what a «موظف» is,
 * and rewriting one would change every other department too. What they can do is let one person in
 * their department do one extra thing, which is what this grants.
 *
 * Three states per permission, and telling them apart is the whole point of the screen:
 *   • from the role — ticked and locked, because it is not this screen's to take away;
 *   • grantable — the caller holds it themselves, so they may pass it on;
 *   • out of reach — greyed, with the reason in the tooltip. A head only ever sees the limit here,
 *     never by pressing save and being refused.
 */

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function UserPermissionsDialog({ user, onClose }: {
  user: AdminUser
  onClose: () => void
}) {
  const toast = useToast()
  const [catalogue, setCatalogue] = useState<PermissionInfo[]>([])
  const [state, setState] = useState<UserPermissions | null>(null)
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let alive = true
    Promise.all([usersApi.permissions(), usersApi.getUserPermissions(user.id)])
      .then(([all, mine]) => {
        if (!alive) return
        setCatalogue(all)
        setState(mine)
        setPicked(new Set(mine.extraPermissionCodes))
      })
      .catch((e) => toast.error(errOf(e, 'تعذّر تحميل الصلاحيات')))
    return () => { alive = false }
  }, [user.id, toast])

  const fromRole = useMemo(() => new Set(state?.rolePermissionCodes ?? []), [state])
  const grantable = useMemo(() => new Set(state?.grantableCodes ?? []), [state])

  /** Codes present in the catalogue, indexed by resource, so a row can look up its own actions. */
  const byResource = useMemo(() => {
    const m = new Map<string, Map<string, string>>()
    catalogue.forEach((p) => {
      if (!m.has(p.resource)) m.set(p.resource, new Map())
      m.get(p.resource)!.set(p.action, p.code)
    })
    return m
  }, [catalogue])

  function toggle(code: string) {
    setPicked((prev) => {
      const next = new Set(prev)
      if (next.has(code)) next.delete(code); else next.add(code)
      return next
    })
  }

  async function save() {
    setBusy(true)
    try {
      const saved = await usersApi.setUserPermissions(user.id, [...picked])
      setState(saved)
      setPicked(new Set(saved.extraPermissionCodes))
      toast.success('حُدّثت صلاحيات المستخدم')
      onClose()
    } catch (e) { toast.error(errOf(e, 'تعذّر حفظ الصلاحيات')) }
    finally { setBusy(false) }
  }

  const changed = useMemo(() => {
    const before = new Set(state?.extraPermissionCodes ?? [])
    return before.size !== picked.size || [...picked].some((c) => !before.has(c))
  }, [state, picked])

  return createPortal(
    <div className="preview-overlay" onClick={() => !busy && onClose()}>
      <div className="preview-modal" onClick={(e) => e.stopPropagation()}
        role="dialog" aria-modal="true" aria-label="صلاحيات المستخدم"
        style={{ width: 'min(920px, 95vw)', height: 'auto', maxHeight: '90vh' }}>
        <header className="preview-head">
          <span className="preview-title">صلاحيات المستخدم · {user.fullName}</span>
          <button type="button" className="btn btn-ghost btn-sm" onClick={onClose}>
            <X size={15} aria-hidden /> إغلاق
          </button>
        </header>

        <div style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
          <p className="muted" style={{ marginTop: 0 }}>
            الصلاحيات المُعلَّمة بقفل يمنحها دور المستخدم ولا تُسحب من هنا. ما عداها صلاحيات إضافية
            لهذا المستخدم وحده. الصلاحيات التي لا تملكها أنت تظهر معطّلة.
          </p>

          {state === null ? <p className="muted">جارٍ التحميل…</p> : (
            <div className="overflow-x-auto">
              {RESOURCE_GROUPS.map((group) => {
                const rows = group.resources.filter((r) => byResource.has(r))
                if (rows.length === 0) return null
                return (
                  <div key={group.label} style={{ marginBottom: '1.25rem' }}>
                    <h4 className="detail-h3" style={{ margin: '0 0 .4rem' }}>{group.label}</h4>
                    <table className="reg-table">
                      <thead><tr>
                        <th style={{ minWidth: 150 }}>المورد</th>
                        {ACTION_ORDER.map((a) => (
                          <th key={a}>{group.actionLabels[a] ?? ACTION_LABELS[a] ?? a}</th>
                        ))}
                      </tr></thead>
                      <tbody>
                        {rows.map((res) => (
                          <tr key={res}>
                            <td>{RESOURCE_LABELS[res] ?? res}</td>
                            {ACTION_ORDER.map((action) => {
                              const code = byResource.get(res)?.get(action)
                              if (!code) return <td key={action} className="muted">—</td>
                              const locked = fromRole.has(code)
                              const allowed = grantable.has(code)
                              return (
                                <td key={action}>
                                  {locked ? (
                                    <span title="يمنحها دور المستخدم — تُعدَّل من إدارة الأدوار">
                                      <Lock size={13} aria-hidden />
                                    </span>
                                  ) : (
                                    <input
                                      type="checkbox"
                                      checked={picked.has(code)}
                                      disabled={!allowed || busy}
                                      onChange={() => toggle(code)}
                                      title={allowed ? code : 'لا يمكنك منح صلاحية لا تملكها'}
                                      aria-label={`${RESOURCE_LABELS[res] ?? res} — ${ACTION_LABELS[action] ?? action}`}
                                    />
                                  )}
                                </td>
                              )
                            })}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
          <button type="button" className="btn btn-primary" disabled={busy || !changed || state === null}
            onClick={save}>{busy ? '…' : 'حفظ الصلاحيات'}</button>
          <button type="button" className="btn btn-ghost" onClick={onClose}>إلغاء</button>
          {/* The endpoint gate reads the signed token, so a change lands for the user when their
              session next issues one. Saying so beats leaving a head to wonder why nothing moved. */}
          <span className="muted" style={{ fontSize: '.75rem', marginInlineStart: 'auto' }}>
            تسري الصلاحيات الجديدة عند تجديد جلسة المستخدم أو إعادة دخوله.
          </span>
        </div>
      </div>
    </div>,
    document.body,
  )
}
