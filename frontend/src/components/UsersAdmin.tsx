import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { usersApi, type AdminUser, type AdminRole, ROLE_LABELS } from '../lib/users'
import { documents, type OrgUnitDto } from '../lib/documents'
import { type Confidentiality } from '../lib/incomingMail'
import { useToast } from './toast'
import '../pages/documents/documents.css'

const CLEARANCE = [
  { v: 0, ar: 'عام' }, { v: 1, ar: 'داخلي' }, { v: 2, ar: 'سري' }, { v: 3, ar: 'سري للغاية' },
]
const roleLabel = (n: string) => ROLE_LABELS[n] ?? n

export default function UsersAdmin() {
  const toast = useToast()
  const [users, setUsers] = useState<AdminUser[]>([])
  const [roles, setRoles] = useState<AdminRole[]>([])
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState<{ id: number; roleIds: number[] } | null>(null)

  const [form, setForm] = useState({
    fullName: '', email: '', password: '', jobTitle: '',
    clearance: 1 as Confidentiality, orgUnitId: '', roleIds: [] as number[],
  })

  const load = useCallback(async () => {
    try {
      const [u, r, o] = await Promise.all([usersApi.list(), usersApi.roles(), documents.orgUnits()])
      setUsers(u); setRoles(r); setUnits(o)
    } catch { toast.error('تعذّر تحميل المستخدمين') }
  }, [toast])

  useEffect(() => { load() }, [load])

  function toggleFormRole(id: number) {
    setForm((f) => ({
      ...f,
      roleIds: f.roleIds.includes(id) ? f.roleIds.filter((x) => x !== id) : [...f.roleIds, id],
    }))
  }

  async function createUser(e: React.FormEvent) {
    e.preventDefault()
    if (!form.fullName.trim() || !form.email.trim()) { toast.error('الاسم والبريد مطلوبان'); return }
    if (form.password.length < 8) { toast.error('كلمة المرور 8 أحرف على الأقل'); return }
    if (form.roleIds.length === 0) { toast.error('اختر دورًا واحدًا على الأقل'); return }
    setBusy(true)
    try {
      await usersApi.create({
        fullName: form.fullName.trim(), email: form.email.trim(), password: form.password,
        jobTitle: form.jobTitle || null, clearance: Number(form.clearance) as Confidentiality,
        orgUnitId: form.orgUnitId ? Number(form.orgUnitId) : null, roleIds: form.roleIds,
      })
      setForm({ fullName: '', email: '', password: '', jobTitle: '', clearance: 1, orgUnitId: '', roleIds: [] })
      await load()
      toast.success('تم إنشاء المستخدم')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر إنشاء المستخدم')
    } finally { setBusy(false) }
  }

  async function toggleActive(u: AdminUser) {
    setBusy(true)
    try { await usersApi.setActive(u.id, !u.isActive); await load(); toast.success(u.isActive ? 'تم التعطيل' : 'تم التفعيل') }
    catch { toast.error('تعذّر تغيير الحالة') } finally { setBusy(false) }
  }

  function startEdit(u: AdminUser) { setEditing({ id: u.id, roleIds: [...u.roleIds] }) }
  function toggleEditRole(id: number) {
    setEditing((e) => e && ({ ...e, roleIds: e.roleIds.includes(id) ? e.roleIds.filter((x) => x !== id) : [...e.roleIds, id] }))
  }
  async function saveRoles() {
    if (!editing) return
    setBusy(true)
    try { await usersApi.setRoles(editing.id, editing.roleIds); setEditing(null); await load(); toast.success('تم تحديث الأدوار') }
    catch { toast.error('تعذّر تحديث الأدوار') } finally { setBusy(false) }
  }

  async function resetPassword(u: AdminUser) {
    const pw = prompt(`كلمة مرور جديدة للمستخدم «${u.fullName}» (8 أحرف على الأقل):`)
    if (!pw) return
    setBusy(true)
    try { await usersApi.resetPassword(u.id, pw); toast.success('تمت إعادة تعيين كلمة المرور') }
    catch { toast.error('تعذّر إعادة التعيين') } finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">إدارة المستخدمين والصلاحيات</h3>

      {/* Add user */}
      <form className="form-grid" onSubmit={createUser}>
        <label className="field"><span>الاسم الكامل *</span>
          <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} /></label>
        <label className="field"><span>البريد الإلكتروني *</span>
          <input type="email" dir="ltr" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></label>
        <label className="field"><span>كلمة المرور *</span>
          <input type="password" dir="ltr" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></label>
        <label className="field"><span>المسمى الوظيفي</span>
          <input value={form.jobTitle} onChange={(e) => setForm({ ...form, jobTitle: e.target.value })} /></label>
        <label className="field"><span>التصنيف الأمني</span>
          <select value={form.clearance} onChange={(e) => setForm({ ...form, clearance: Number(e.target.value) as Confidentiality })}>
            {CLEARANCE.map((c) => <option key={c.v} value={c.v}>{c.ar}</option>)}
          </select></label>
        <label className="field"><span>الوحدة التنظيمية</span>
          <select value={form.orgUnitId} onChange={(e) => setForm({ ...form, orgUnitId: e.target.value })}>
            <option value="">— بدون —</option>
            {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select></label>
        <div className="field field--wide">
          <span>الأدوار (الصلاحيات) *</span>
          <div className="role-checks">
            {roles.map((r) => (
              <label key={r.id} className={`role-chip ${form.roleIds.includes(r.id) ? 'is-on' : ''}`}>
                <input type="checkbox" checked={form.roleIds.includes(r.id)} onChange={() => toggleFormRole(r.id)} />
                {roleLabel(r.name)}
              </label>
            ))}
          </div>
        </div>
        <div className="form-actions">
          <button className="btn btn-primary" disabled={busy}>+ إضافة مستخدم</button>
        </div>
      </form>

      {/* Users table */}
      <div className="table-scroll" style={{ marginTop: '1.2rem' }}>
      <table className="reg-table users-table">
        <thead>
          <tr><th>الاسم</th><th>البريد</th><th>الأدوار</th><th>التصنيف</th><th>الحالة</th><th>إجراءات</th></tr>
        </thead>
        <tbody>
          {users.length === 0 && <tr><td colSpan={6} className="reg-empty">لا يوجد مستخدمون</td></tr>}
          {users.map((u) => (
            <tr key={u.id}>
              <td>{u.fullName}</td>
              <td className="mono" dir="ltr">{u.email}</td>
              <td>
                {editing?.id === u.id ? (
                  <div className="role-checks">
                    {roles.map((r) => (
                      <label key={r.id} className={`role-chip ${editing.roleIds.includes(r.id) ? 'is-on' : ''}`}>
                        <input type="checkbox" checked={editing.roleIds.includes(r.id)} onChange={() => toggleEditRole(r.id)} />
                        {roleLabel(r.name)}
                      </label>
                    ))}
                  </div>
                ) : (
                  u.roles.map((r) => <span key={r} className="badge internal" style={{ marginInlineEnd: 4 }}>{roleLabel(r)}</span>)
                )}
              </td>
              <td>{u.clearance}</td>
              <td><span className={`status-pill ${u.isActive ? 's-active' : 's-onhold'}`}>{u.isActive ? 'مفعّل' : 'معطّل'}</span></td>
              <td className="user-actions">
                {editing?.id === u.id ? (
                  <>
                    <button className="btn btn-primary btn-sm" disabled={busy} onClick={saveRoles}>حفظ</button>{' '}
                    <button className="btn btn-ghost btn-sm" onClick={() => setEditing(null)}>إلغاء</button>
                  </>
                ) : (
                  <>
                    <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => startEdit(u)}>الأدوار</button>{' '}
                    <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => toggleActive(u)}>{u.isActive ? 'تعطيل' : 'تفعيل'}</button>{' '}
                    <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => resetPassword(u)}>كلمة المرور</button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>
    </motion.section>
  )
}
