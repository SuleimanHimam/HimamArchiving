import { useEffect, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { motion, AnimatePresence } from 'motion/react'
import { usersApi, type AdminUser, type AdminRole, ROLE_LABELS } from '../lib/users'
import { documents, type OrgUnitDto } from '../lib/documents'
import { type Confidentiality } from '../lib/incomingMail'
import { isStrongPassword } from '../lib/passwordStrength'
import PasswordStrengthMeter from './PasswordStrengthMeter'
import PasswordInput from './PasswordInput'
import { useToast } from './toast'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

// ── Arabic → Latin transliteration for auto-email ─────────────────────────
const TRANS: Record<string, string> = {
  'ا':'a','أ':'a','إ':'i','آ':'aa','ء':'a','ئ':'y','ؤ':'w',
  'ب':'b','ت':'t','ث':'th','ج':'j','ح':'h','خ':'kh',
  'د':'d','ذ':'dh','ر':'r','ز':'z','س':'s','ش':'sh',
  'ص':'s','ض':'d','ط':'t','ظ':'z','ع':'a','غ':'gh',
  'ف':'f','ق':'q','ك':'k','ل':'l','م':'m','ن':'n',
  'ه':'h','و':'w','ي':'y','ى':'a','ة':'a',
}
function latinize(s: string) {
  return s
    .replace(/[ً-ٰٟ]/g, '') // strip diacritics
    .replace(/ال/g, '')                    // strip ال
    .split('').map((c) => TRANS[c] ?? (/[a-z0-9]/i.test(c) ? c.toLowerCase() : '')).join('')
}
function buildEmail(first: string, family: string, domain: string) {
  const f = latinize(first.trim())
  const l = latinize(family.trim())
  if (!f && !l) return ''
  const user = [f, l].filter(Boolean).join('.')
  return `${user}@${domain}`
}

// ── Helpers ────────────────────────────────────────────────────────────────
const CLEARANCE = [
  { v: 0, ar: 'عام' }, { v: 1, ar: 'داخلي' },
  { v: 2, ar: 'سري' }, { v: 3, ar: 'سري للغاية' },
]
const roleLabel = (n: string) => ROLE_LABELS[n] ?? n
const GENDER_LABELS: Record<string, string> = { Male: 'ذكر', Female: 'أنثى', NotSpecified: '—' }

// Default email domain inferred from admin email (or fallback)
function defaultDomain() {
  const raw = localStorage.getItem('archiving.user')
  try {
    const u = raw ? JSON.parse(raw) : null
    const em: string = u?.email ?? ''
    const at = em.indexOf('@')
    return at > 0 ? em.slice(at + 1) : 'archiving.local'
  } catch { return 'archiving.local' }
}

const DOMAIN = defaultDomain()

// ── Form shape ─────────────────────────────────────────────────────────────
interface FormState {
  firstName: string; secondName: string; thirdName: string; familyName: string
  email: string; emailAuto: boolean
  password: string; confirmPassword: string
  gender: 0 | 1 | 2
  nationalId: string
  jobTitle: string
  clearance: Confidentiality
  orgUnitId: string
  roleId: number
}
const EMPTY: FormState = {
  firstName: '', secondName: '', thirdName: '', familyName: '',
  email: '', emailAuto: true,
  password: '', confirmPassword: '',
  gender: 0, nationalId: '',
  jobTitle: '', clearance: 1, orgUnitId: '', roleId: 0,
}

export default function UsersAdmin() {
  const toast = useToast()
  const [users, setUsers]       = useState<AdminUser[]>([])
  const [roles, setRoles]       = useState<AdminRole[]>([])
  const [units, setUnits]       = useState<OrgUnitDto[]>([])
  const [busy, setBusy]         = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editRoleId, setEditRoleId] = useState<number>(0)
  const [form, setForm]         = useState<FormState>(EMPTY)
  const [resetTarget, setResetTarget] = useState<AdminUser | null>(null)
  const [resetPw, setResetPw]         = useState('')
  const [resetConfirmPw, setResetConfirmPw] = useState('')

  const load = useCallback(async () => {
    try {
      const [u, r, o] = await Promise.all([usersApi.list(), usersApi.roles(), documents.orgUnits()])
      setUsers(u); setRoles(r); setUnits(o)
    } catch { toast.error('تعذّر تحميل المستخدمين') }
  }, [toast])

  useEffect(() => { load() }, [load])

  // Auto-generate email whenever name parts change
  function updateName(patch: Partial<FormState>) {
    setForm((prev) => {
      const next = { ...prev, ...patch }
      if (next.emailAuto) {
        next.email = buildEmail(next.firstName, next.familyName, DOMAIN)
      }
      return next
    })
  }

  function setEmail(val: string) {
    setForm((prev) => ({ ...prev, email: val, emailAuto: false }))
  }
  function resetEmailAuto() {
    setForm((prev) => {
      const email = buildEmail(prev.firstName, prev.familyName, DOMAIN)
      return { ...prev, email, emailAuto: true }
    })
  }

  const pwMatch = form.password === form.confirmPassword

  async function createUser(e: React.FormEvent) {
    e.preventDefault()
    if (!form.firstName.trim() || !form.familyName.trim()) { toast.error('الاسم الأول واسم العائلة مطلوبان'); return }
    if (!form.email.trim())      { toast.error('البريد الإلكتروني مطلوب'); return }
    if (!isStrongPassword(form.password)) { toast.error('كلمة المرور غير قوية بما يكفي — راجع الشروط أسفل الحقل'); return }
    if (!pwMatch)                { toast.error('كلمتا المرور غير متطابقتين'); return }
    if (!form.roleId)            { toast.error('اختر دورًا'); return }

    const parts = [form.firstName, form.secondName, form.thirdName, form.familyName].filter(Boolean)
    const fullName = parts.join(' ')

    setBusy(true)
    try {
      await usersApi.create({
        fullName,
        firstName:  form.firstName.trim()  || null,
        secondName: form.secondName.trim() || null,
        thirdName:  form.thirdName.trim()  || null,
        familyName: form.familyName.trim() || null,
        gender:     form.gender,
        nationalId: form.nationalId.trim() || null,
        email:      form.email.trim(),
        password:   form.password,
        jobTitle:   form.jobTitle || null,
        clearance:  Number(form.clearance) as Confidentiality,
        orgUnitId:  form.orgUnitId ? Number(form.orgUnitId) : null,
        roleIds:    [form.roleId],
      })
      setForm(EMPTY)
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

  function startEdit(u: AdminUser) { setEditingId(u.id); setEditRoleId(u.roleIds[0] ?? 0) }

  async function saveRole() {
    if (!editingId || !editRoleId) return
    setBusy(true)
    try { await usersApi.setRole(editingId, editRoleId); setEditingId(null); await load(); toast.success('تم تحديث الدور') }
    catch { toast.error('تعذّر تحديث الدور') } finally { setBusy(false) }
  }

  function openResetPassword(u: AdminUser) {
    setResetTarget(u); setResetPw(''); setResetConfirmPw('')
  }
  function closeResetPassword() { setResetTarget(null) }

  const resetPwMatch = resetPw === resetConfirmPw

  async function submitResetPassword(e: React.FormEvent) {
    e.preventDefault()
    if (!resetTarget) return
    if (!isStrongPassword(resetPw)) { toast.error('كلمة المرور غير قوية بما يكفي — راجع الشروط أسفل الحقل'); return }
    if (!resetPwMatch)              { toast.error('كلمتا المرور غير متطابقتين'); return }

    setBusy(true)
    try {
      await usersApi.resetPassword(resetTarget.id, resetPw)
      toast.success('تمت إعادة تعيين كلمة المرور')
      setResetTarget(null)
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر إعادة التعيين')
    } finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">إضافة مستخدم جديد</h3>

      <form className="form-grid" onSubmit={createUser}>

        {/* ── Name components ── */}
        <label className="field">
          <span>الاسم الأول *</span>
          <input value={form.firstName} onChange={(e) => updateName({ firstName: e.target.value })} />
        </label>
        <label className="field">
          <span>اسم الأب</span>
          <input value={form.secondName} onChange={(e) => updateName({ secondName: e.target.value })} />
        </label>
        <label className="field">
          <span>اسم الجد</span>
          <input value={form.thirdName} onChange={(e) => updateName({ thirdName: e.target.value })} />
        </label>
        <label className="field">
          <span>اسم العائلة *</span>
          <input value={form.familyName} onChange={(e) => updateName({ familyName: e.target.value })} />
        </label>

        {/* ── Gender ── */}
        <div className="field">
          <span>الجنس</span>
          <div className="gender-radios">
            <label className="gender-radio">
              <input
                type="radio" name="gender" value={1}
                checked={form.gender === 1}
                onChange={() => setForm((p) => ({ ...p, gender: 1 }))}
              />
              <span>ذكر</span>
            </label>
            <label className="gender-radio">
              <input
                type="radio" name="gender" value={2}
                checked={form.gender === 2}
                onChange={() => setForm((p) => ({ ...p, gender: 2 }))}
              />
              <span>أنثى</span>
            </label>
          </div>
        </div>

        {/* ── National ID ── */}
        <label className="field">
          <span>رقم الهوية الوطنية</span>
          <input
            type="text" inputMode="numeric" dir="ltr"
            maxLength={10}
            placeholder="0000000000"
            value={form.nationalId}
            onChange={(e) => setForm((p) => ({ ...p, nationalId: e.target.value.replace(/\D/g, '') }))}
          />
        </label>

        {/* ── Email (auto-generated) ── */}
        <label className="field">
          <span>
            البريد الإلكتروني *
            {!form.emailAuto && (
              <button
                type="button"
                className="email-auto-btn"
                onClick={resetEmailAuto}
                title="إعادة التوليد التلقائي من الاسم"
              >↻ توليد تلقائي</button>
            )}
          </span>
          <input
            type="email" dir="ltr"
            value={form.email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="name.family@domain"
          />
          {form.emailAuto && form.email && (
            <span className="email-auto-note">✦ تم التوليد من الاسم</span>
          )}
        </label>

        {/* ── Password ── */}
        <label className="field">
          <span>كلمة المرور *</span>
          <PasswordInput
            dir="ltr"
            value={form.password}
            onChange={(e) => setForm((p) => ({ ...p, password: e.target.value }))}
          />
          <PasswordStrengthMeter password={form.password} />
        </label>
        <label className="field">
          <span>تأكيد كلمة المرور *</span>
          <PasswordInput
            dir="ltr"
            value={form.confirmPassword}
            placeholder="أعد كتابة كلمة المرور"
            onChange={(e) => setForm((p) => ({ ...p, confirmPassword: e.target.value }))}
          />
          {form.confirmPassword.length > 0 && (
            <div className="pw-match-bar">
              <div className={`pw-match-bar__fill ${pwMatch ? 'pw-match--ok' : 'pw-match--err'}`} />
              <span className={`pw-match-bar__label ${pwMatch ? 'pw-match--ok' : 'pw-match--err'}`}>
                {pwMatch ? '✓ كلمتا المرور متطابقتان' : '✗ كلمتا المرور غير متطابقتين'}
              </span>
            </div>
          )}
        </label>

        {/* ── Optional fields ── */}
        <label className="field">
          <span>المسمى الوظيفي</span>
          <input value={form.jobTitle} onChange={(e) => setForm((p) => ({ ...p, jobTitle: e.target.value }))} />
        </label>
        <label className="field">
          <span>التصنيف الأمني</span>
          <select value={form.clearance} onChange={(e) => setForm((p) => ({ ...p, clearance: Number(e.target.value) as Confidentiality }))}>
            {CLEARANCE.map((c) => <option key={c.v} value={c.v}>{c.ar}</option>)}
          </select>
        </label>
        <label className="field">
          <span>الوحدة التنظيمية</span>
          <select value={form.orgUnitId} onChange={(e) => setForm((p) => ({ ...p, orgUnitId: e.target.value }))}>
            <option value="">— بدون —</option>
            {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select>
        </label>
        <label className="field">
          <span>الدور *</span>
          <select value={form.roleId} onChange={(e) => setForm((p) => ({ ...p, roleId: Number(e.target.value) }))}>
            <option value={0}>— اختر دورًا —</option>
            {roles.map((r) => <option key={r.id} value={r.id}>{roleLabel(r.name)}</option>)}
          </select>
        </label>

        <div className="form-actions">
          <button className="btn btn-primary" disabled={busy}>+ إضافة مستخدم</button>
        </div>
      </form>

      {/* ── User table ── */}
      <h3 className="detail-h3" style={{ marginTop: '1.5rem' }}>قائمة المستخدمين</h3>
      <div className="table-scroll">
        <table className="reg-table users-table">
          <thead>
            <tr>
              <th>الاسم الكامل</th>
              <th>البريد</th>
              <th>الجنس</th>
              <th>رقم الهوية</th>
              <th>الدور</th>
              <th>التصنيف</th>
              <th>الحالة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 && <tr><td colSpan={8} className="reg-empty">لا يوجد مستخدمون</td></tr>}
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.fullName}</td>
                <td className="mono" dir="ltr">{u.email}</td>
                <td>{GENDER_LABELS[u.gender] ?? '—'}</td>
                <td className="mono">{u.nationalId ?? '—'}</td>
                <td>
                  {editingId === u.id ? (
                    <select
                      className="filters__status"
                      style={{ minWidth: 160 }}
                      value={editRoleId}
                      onChange={(e) => setEditRoleId(Number(e.target.value))}
                    >
                      <option value={0}>— اختر —</option>
                      {roles.map((r) => <option key={r.id} value={r.id}>{roleLabel(r.name)}</option>)}
                    </select>
                  ) : (
                    u.roles.map((r) => (
                      <span key={r} className="badge internal" style={{ marginInlineEnd: 4 }}>{roleLabel(r)}</span>
                    ))
                  )}
                </td>
                <td>{u.clearance}</td>
                <td><span className={`status-pill ${u.isActive ? 's-active' : 's-onhold'}`}>{u.isActive ? 'مفعّل' : 'معطّل'}</span></td>
                <td className="user-actions">
                  {editingId === u.id ? (
                    <>
                      <button className="btn btn-primary btn-sm" disabled={busy || !editRoleId} onClick={saveRole}>حفظ</button>{' '}
                      <button className="btn btn-ghost btn-sm" onClick={() => setEditingId(null)}>إلغاء</button>
                    </>
                  ) : (
                    <>
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => startEdit(u)}>الدور</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => toggleActive(u)}>{u.isActive ? 'تعطيل' : 'تفعيل'}</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => openResetPassword(u)}>كلمة المرور</button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {createPortal(
        <AnimatePresence>
          {resetTarget && (
            <motion.div
              className="pwdlg__backdrop"
              initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
              transition={{ duration: 0.18 }}
              onClick={closeResetPassword}
            >
              <motion.div
                className="pwdlg"
                role="dialog" aria-modal="true"
                initial={{ opacity: 0, scale: 0.95, y: 12 }}
                animate={{ opacity: 1, scale: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.95, y: 12 }}
                transition={{ duration: 0.2 }}
                onClick={(e) => e.stopPropagation()}
              >
                <h3 className="detail-h3">إعادة تعيين كلمة المرور</h3>
                <p className="pwdlg__sub">للمستخدم «{resetTarget.fullName}»</p>

                <form className="form-grid" onSubmit={submitResetPassword}>
                  <label className="field">
                    <span>كلمة المرور الجديدة *</span>
                    <PasswordInput
                      dir="ltr" autoFocus
                      value={resetPw}
                      onChange={(e) => setResetPw(e.target.value)}
                    />
                    <PasswordStrengthMeter password={resetPw} />
                  </label>
                  <label className="field">
                    <span>تأكيد كلمة المرور *</span>
                    <PasswordInput
                      dir="ltr"
                      value={resetConfirmPw}
                      onChange={(e) => setResetConfirmPw(e.target.value)}
                    />
                    {resetConfirmPw.length > 0 && (
                      <div className="pw-match-bar">
                        <div className={`pw-match-bar__fill ${resetPwMatch ? 'pw-match--ok' : 'pw-match--err'}`} />
                        <span className={`pw-match-bar__label ${resetPwMatch ? 'pw-match--ok' : 'pw-match--err'}`}>
                          {resetPwMatch ? '✓ كلمتا المرور متطابقتان' : '✗ كلمتا المرور غير متطابقتين'}
                        </span>
                      </div>
                    )}
                  </label>

                  <div className="form-actions">
                    <button className="btn btn-primary" disabled={busy}>حفظ كلمة المرور</button>
                    <button type="button" className="btn btn-ghost" onClick={closeResetPassword}>إلغاء</button>
                  </div>
                </form>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>,
        document.body,
      )}
    </motion.section>
  )
}
