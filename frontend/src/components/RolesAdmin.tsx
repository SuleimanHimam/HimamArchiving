import { useEffect, useState, useCallback } from 'react'
import { usersApi, type AdminRole, type RolePermissions, type PermissionInfo, ROLE_LABELS, RESOURCE_LABELS, ACTION_LABELS, ACTION_ORDER, RESOURCE_GROUPS } from '../lib/users'
import { useToast } from './toast'
import './rolesadmin.css'

const label = (n: string) => ROLE_LABELS[n] ?? n

export default function RolesAdmin() {
  const toast = useToast()
  const [roles, setRoles] = useState<AdminRole[]>([])
  const [perms, setPerms] = useState<PermissionInfo[]>([])
  const [selected, setSelected] = useState<RolePermissions | null>(null)
  const [matrix, setMatrix] = useState<Record<string, boolean>>({})
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState(false)
  const [creating, setCreating] = useState(false)
  const [newName, setNewName] = useState('')
  const [newDesc, setNewDesc] = useState('')

  const load = useCallback(async () => {
    try {
      const [r, p] = await Promise.all([usersApi.roles(), usersApi.permissions()])
      setRoles(r); setPerms(p)
    } catch { toast.error('تعذّر تحميل الأدوار') }
  }, [toast])

  useEffect(() => { load() }, [load])

  // Actions in a fixed logical order; fall back to any extras not in the list
  const knownActions = new Set(perms.map((p) => p.action))
  const actions = [...ACTION_ORDER.filter((a) => knownActions.has(a)),
                   ...[...knownActions].filter((a) => !ACTION_ORDER.includes(a))]

  async function selectRole(role: AdminRole) {
    setBusy(true)
    try {
      const rp = await usersApi.getRolePermissions(role.id)
      setSelected(rp)
      const m: Record<string, boolean> = {}
      perms.forEach((p) => { m[p.code] = false })
      rp.permissionCodes.forEach((c) => { m[c] = true })
      setMatrix(m)
      setDirty(false)
    } catch { toast.error('تعذّر تحميل صلاحيات الدور') }
    finally { setBusy(false) }
  }

  function toggle(code: string) {
    setMatrix((m) => ({ ...m, [code]: !m[code] }))
    setDirty(true)
  }

  function toggleResource(resource: string, forceVal?: boolean) {
    const codes = perms.filter((p) => p.resource === resource).map((p) => p.code)
    const allOn = codes.every((c) => matrix[c])
    const next = forceVal ?? !allOn
    setMatrix((m) => {
      const updated = { ...m }
      codes.forEach((c) => { updated[c] = next })
      return updated
    })
    setDirty(true)
  }

  function toggleAction(action: string, forceVal?: boolean) {
    const codes = perms.filter((p) => p.action === action).map((p) => p.code)
    const allOn = codes.every((c) => matrix[c])
    const next = forceVal ?? !allOn
    setMatrix((m) => {
      const updated = { ...m }
      codes.forEach((c) => { updated[c] = next })
      return updated
    })
    setDirty(true)
  }

  function toggleAll(val: boolean) {
    setMatrix((m) => Object.fromEntries(Object.keys(m).map((k) => [k, val])))
    setDirty(true)
  }

  async function save() {
    if (!selected) return
    setBusy(true)
    try {
      const codes = Object.entries(matrix).filter(([, v]) => v).map(([k]) => k)
      const rp = await usersApi.setRolePermissions(selected.id, codes)
      setSelected(rp)
      setDirty(false)
      toast.success('تم حفظ صلاحيات الدور')
    } catch { toast.error('تعذّر حفظ الصلاحيات') }
    finally { setBusy(false) }
  }

  async function resetToDefaults() {
    if (!selected) return
    if (!confirm('إعادة تعيين صلاحيات هذا الدور إلى القيم الافتراضية؟ سيتم فقدان أي تعديلات.')) return
    setBusy(true)
    try {
      const rp = await usersApi.resetRolePermissions(selected.id)
      setSelected(rp)
      const m: Record<string, boolean> = {}
      perms.forEach((p) => { m[p.code] = false })
      rp.permissionCodes.forEach((c) => { m[c] = true })
      setMatrix(m)
      setDirty(false)
      toast.success('تمت إعادة التعيين إلى الصلاحيات الافتراضية')
    } catch { toast.error('تعذّرت إعادة التعيين') }
    finally { setBusy(false) }
  }

  async function createRole(e: React.FormEvent) {
    e.preventDefault()
    if (!newName.trim()) { toast.error('اسم الدور مطلوب'); return }
    setBusy(true)
    try {
      await usersApi.createRole(newName.trim(), newDesc.trim())
      setNewName(''); setNewDesc(''); setCreating(false)
      await load()
      toast.success('تم إنشاء الدور')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر إنشاء الدور')
    } finally { setBusy(false) }
  }

  async function deleteRole(role: AdminRole) {
    if (!confirm(`حذف الدور «${label(role.name)}»؟`)) return
    setBusy(true)
    try {
      await usersApi.deleteRole(role.id)
      if (selected?.id === role.id) { setSelected(null); setMatrix({}) }
      await load()
      toast.success('تم حذف الدور')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر حذف الدور')
    } finally { setBusy(false) }
  }

  return (
    <div className="roles-layout">
      {/* Left: role list */}
      <aside className="roles-aside">
        <div className="roles-aside__head">
          <span className="kicker">ROLES · الأدوار</span>
          <button className="btn btn-primary btn-sm" onClick={() => setCreating((v) => !v)}>
            {creating ? '✕' : '+ دور جديد'}
          </button>
        </div>

        {creating && (
          <form className="roles-new" onSubmit={createRole}>
            <input
              className="roles-new__input" placeholder="اسم الدور *"
              value={newName} onChange={(e) => setNewName(e.target.value)}
            />
            <input
              className="roles-new__input" placeholder="الوصف (اختياري)"
              value={newDesc} onChange={(e) => setNewDesc(e.target.value)}
            />
            <button className="btn btn-seal btn-sm" disabled={busy}>إنشاء</button>
          </form>
        )}

        <ul className="roles-list">
          {roles.map((r) => (
            <li key={r.id}
              className={`roles-list__item ${selected?.id === r.id ? 'is-active' : ''}`}
              onClick={() => selectRole(r)}
            >
              <div className="roles-list__name">{label(r.name)}</div>
              {r.description && <div className="roles-list__desc">{r.description}</div>}
              <div className="roles-list__actions" onClick={(e) => e.stopPropagation()}>
                <button
                  className="roles-list__del"
                  disabled={busy}
                  title="حذف الدور"
                  onClick={() => deleteRole(r)}
                >✕</button>
              </div>
            </li>
          ))}
        </ul>
      </aside>

      {/* Right: permission matrix */}
      <div className="roles-matrix-wrap">
        {!selected ? (
          <div className="roles-empty">
            <span>اختر دورًا من القائمة لعرض وتعديل صلاحياته</span>
          </div>
        ) : (
          <>
            <div className="roles-matrix-head">
              <div>
                <span className="kicker">PERMISSIONS · الصلاحيات</span>
                <h3 className="roles-matrix-title">{label(selected.name)}</h3>
                {selected.description && <p className="roles-matrix-desc">{selected.description}</p>}
                {selected.isSystem && (
                  <p className="roles-matrix-system">⚙ دور مدمج — لا يمكن حذفه</p>
                )}
              </div>
              <div className="roles-matrix-top-actions">
                <button className="btn btn-ghost btn-sm" onClick={() => toggleAll(true)}>تمكين الكل</button>
                <button className="btn btn-ghost btn-sm" onClick={() => toggleAll(false)}>تعطيل الكل</button>
                {selected.isSystem && (
                  <button className="btn btn-ghost btn-sm roles-reset-btn" disabled={busy} onClick={resetToDefaults}>
                    ↺ إعادة الافتراضي
                  </button>
                )}
                <button className="btn btn-seal" disabled={!dirty || busy} onClick={save}>
                  {busy ? 'جارٍ الحفظ...' : 'حفظ الصلاحيات'}
                </button>
              </div>
            </div>

            <div className="roles-matrix-scroll">
              <table className="perm-table">
                <thead>
                  <tr>
                    <th className="perm-th perm-th--resource">الوحدة / الموديول</th>
                    {actions.map((a) => (
                      <th key={a} className="perm-th perm-th--action">
                        <button
                          className="perm-col-toggle"
                          title={`تبديل كل "${ACTION_LABELS[a] ?? a}"`}
                          onClick={() => toggleAction(a)}
                        >
                          {ACTION_LABELS[a] ?? a}
                        </button>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {RESOURCE_GROUPS.map((group) => {
                    const groupResources = group.resources.filter((r) =>
                      perms.some((p) => p.resource === r)
                    )
                    if (groupResources.length === 0) return null
                    return (
                      <>
                        <tr key={`grp-${group.label}`} className="perm-group-row">
                          <td colSpan={actions.length + 1} className="perm-group-cell">
                            {group.label}
                          </td>
                        </tr>
                        <tr key={`subhdr-${group.label}`} className="perm-subhdr-row">
                          <th className="perm-th perm-th--resource perm-th--sub">الوحدة</th>
                          {actions.map((a) => (
                            <th key={a} className="perm-th perm-th--action perm-th--sub">
                              {group.actionLabels[a] ?? ACTION_LABELS[a] ?? a}
                            </th>
                          ))}
                        </tr>
                        {groupResources.map((res) => {
                          const rowCodes = perms.filter((p) => p.resource === res).map((p) => p.code)
                          const allOn = rowCodes.every((c) => matrix[c])
                          const someOn = rowCodes.some((c) => matrix[c])
                          return (
                            <tr key={res} className="perm-row">
                              <td className="perm-td perm-td--resource">
                                <button
                                  className="perm-row-toggle"
                                  onClick={() => toggleResource(res)}
                                >
                                  <span className={`perm-row-indicator ${allOn ? 'is-all' : someOn ? 'is-some' : ''}`} />
                                  {RESOURCE_LABELS[res] ?? res}
                                </button>
                              </td>
                              {actions.map((a) => {
                                const code = `${res}.${a}`
                                const on = matrix[code] ?? false
                                const exists = perms.some((p) => p.code === code)
                                return (
                                  <td key={a} className="perm-td perm-td--cell">
                                    {exists ? (
                                      <button
                                        className={`perm-cell ${on ? 'is-on' : 'is-off'}`}
                                        onClick={() => toggle(code)}
                                        title={on ? 'مُفعَّل — انقر للتعطيل' : 'مُعطَّل — انقر للتفعيل'}
                                      >
                                        {on ? '✓' : '✗'}
                                      </button>
                                    ) : (
                                      <span className="perm-cell perm-cell--na">—</span>
                                    )}
                                  </td>
                                )
                              })}
                            </tr>
                          )
                        })}
                      </>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
