import { useEffect, useState, useCallback, useMemo, Fragment } from 'react'
import { ArrowRight, RefreshCw, Check, X, Search } from 'lucide-react'
import { createPortal } from 'react-dom'
import { motion, AnimatePresence } from 'motion/react'
import { usersApi, type AdminUser, type AdminRole, roleLabel } from '../lib/users'
import { auth } from '../lib/auth'

/** The only role a department head may assign — mirrors UserAdminService.StaffRole. */
const STAFF_ROLE = 'Employee'
import { documents, type OrgUnitDto } from '../lib/documents'
import { groups as groupsApi, type Group } from '../lib/groups'
import { type Confidentiality, clearanceLabel, clearanceClass } from '../lib/shared'
import { isStrongPassword } from '../lib/passwordStrength'
import PasswordStrengthMeter from './PasswordStrengthMeter'
import UserPermissionsDialog from './UserPermissionsDialog'
import UserSectionsDialog from './UserSectionsDialog'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from './ui/expandable-row'
import { LabeledInput, LabeledSelect, LabeledPassword } from './ui/labeled-input'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { ColumnHeaderMenu } from './ui/column-filter'
import { Tabs, TabsContent, TabsList, TabsTrigger } from './ui/tabs'
import { useToast } from './toast'
import '../styles/forms.css'
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

/** Search key: case-folded, diacritic-free, with the letters Arabic typists spell either way
 *  (أ/إ/آ، ى، ة) folded together — so «مصطفى» finds «مصطفي» and «احمد» finds «أحمد». */
function normalizeAr(s: string | null | undefined) {
  return (s ?? '')
    .toLowerCase()
    .replace(/[ً-ٰٟـ]/g, '')   // diacritics + tatweel
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .replace(/ة/g, 'ه')
    .trim()
}

// ── Helpers ────────────────────────────────────────────────────────────────
const CLEARANCE = [
  { v: 0, ar: 'عام' }, { v: 1, ar: 'داخلي' },
  { v: 2, ar: 'سري' }, { v: 3, ar: 'سري للغاية' },
]
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
  const { toggle, isExpanded } = useExpandedRow<number>()
  const toast = useToast()
  const [tab, setTab]           = useState<'list' | 'add'>('list')
  const [users, setUsers]       = useState<AdminUser[]>([])
  const [roles, setRoles]       = useState<AdminRole[]>([])
  const [units, setUnits]       = useState<OrgUnitDto[]>([])
  const [busy, setBusy]         = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editRoleId, setEditRoleId] = useState<number>(0)
  const [form, setForm]         = useState<FormState>(EMPTY)
  const [q, setQ]               = useState('')
  const [roleFilter, setRoleFilter]     = useState('')
  /* Active and deactivated are two lists, not one list with a filter on it. A deactivated account
     is not a staff member you might scroll past — it is a former one, and mixing them meant the
     count above the table answered "how many accounts exist" when the question being asked was
     "how many people work here". The old «كل الحالات» dropdown is gone: two controls for the same
     axis is where "why is this person missing" comes from. */
  const [statusTab, setStatusTab] = useState<'active' | 'disabled'>('active')
  const [clearFilter, setClearFilter]   = useState('')
  /* Group membership per user, fetched when a row is expanded rather than for all 120 rows
     up front. Membership decides which groups that person may address in internal mail, so
     it belongs next to their roles. */
  const [userGroups, setUserGroups] = useState<Record<number, Group[]>>({})
  const [resetTarget, setResetTarget] = useState<AdminUser | null>(null)
  const [resetPw, setResetPw]         = useState('')
  const [resetConfirmPw, setResetConfirmPw] = useState('')

  /* Extra departments. Only the system administrator may widen someone's reach, so the control
     is not rendered for anyone else — the server refuses it either way, but a button that always
     fails is worse than no button. */
  const canGrantDepartments = auth.isSystemAdmin()

  /* The roles a department head may hand out: «موظف» and nothing else.
     Every other role reaches past the department — Archive Officer carries the custody right and
     with it every document in the institution, Manager carries the department's approvals, System
     Administrator carries the lot — so granting one is the administrator's decision. The server
     refuses the rest either way; this keeps them off the menu so the refusal is never something
     a head discovers by trying. */
  const canAssignAnyRole = auth.isSystemAdmin()

  /** The departments this caller may file a new user into: everything for the administrator,
   *  their own plus any granted to them for anyone else. Read off their own row in the list,
   *  which is the same answer the server computes. */
  const myUnits = useMemo(() => {
    if (canAssignAnyRole) return units
    const me = users.find((u) => u.id === auth.getUser()?.id)
    const mine = [me?.orgUnitId, ...(me?.extraOrgUnitIds ?? [])].filter((x): x is number => x != null)
    return units.filter((u) => mine.includes(u.id))
  }, [units, users, canAssignAnyRole])
  const assignableRoles = useMemo(
    () => (canAssignAnyRole ? roles : roles.filter((r) => r.name === STAFF_ROLE)),
    [roles, canAssignAnyRole],
  )
  const [deptTarget, setDeptTarget] = useState<AdminUser | null>(null)
  const [deptPicked, setDeptPicked] = useState<number[]>([])
  /* Per-person permissions. Unlike roles, this one IS a head's to operate: the server lets them
     pass on only what they hold themselves, inside their own department. */
  const [permTarget, setPermTarget] = useState<AdminUser | null>(null)
  /* Which sidebar sections one person sees. Presentation only, so a head may set it for anyone in
     their department without the ceiling the permission dialog applies. */
  const [sectionTarget, setSectionTarget] = useState<AdminUser | null>(null)

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
    // A head does not choose a role, so there is nothing for them to have left blank: the field
    // is fixed to «موظف» and that is what gets sent.
    const roleId = canAssignAnyRole ? form.roleId : assignableRoles[0]?.id ?? 0
    if (!roleId) { toast.error(canAssignAnyRole ? 'اختر دورًا' : 'دور «موظف» غير معرَّف في النظام'); return }

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
        // With one department the field is fixed rather than selected, so it is read from there.
        orgUnitId:  form.orgUnitId ? Number(form.orgUnitId) : myUnits.length === 1 ? myUnits[0].id : null,
        roleIds:    [roleId],
      })
      setForm(EMPTY)
      await load()
      setTab('list')
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

  function openDepartments(u: AdminUser) { setDeptTarget(u); setDeptPicked(u.extraOrgUnitIds ?? []) }

  async function saveDepartments() {
    if (!deptTarget) return
    setBusy(true)
    try {
      await usersApi.setDepartments(deptTarget.id, deptPicked)
      setDeptTarget(null)
      await load()
      toast.success('تم تحديث الأقسام')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر تحديث الأقسام')
    } finally { setBusy(false) }
  }

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

  /* Free-text search + the three filters that people actually ask for (دور، حالة، تصنيف).
     The per-column menus stay — they answer "show me only these values"; this row answers
     "find this person", which no checklist does once the list runs to a hundred rows. */
  /** Everyone on the tab currently open, before the search box and the filters narrow it. */
  const inTab = useMemo(
    () => users.filter((u) => (statusTab === 'active' ? u.isActive : !u.isActive)),
    [users, statusTab],
  )

  const filtered = useMemo(() => {
    const needle = normalizeAr(q)
    return inTab.filter((u) => {
      if (needle && !normalizeAr([u.fullName, u.email, u.nationalId, u.jobTitle].join(' ')).includes(needle)) return false
      if (roleFilter && !u.roles.includes(roleFilter)) return false
      if (clearFilter && String(u.clearance) !== clearFilter) return false
      return true
    })
  }, [inTab, q, roleFilter, clearFilter])

  const disabledCount = useMemo(() => users.filter((u) => !u.isActive).length, [users])

  const filtering = !!(q || roleFilter || clearFilter)
  function clearFilters() { setQ(''); setRoleFilter(''); setClearFilter('') }

  // Excel-style per-column sort + value checklist over the loaded users.
  const columns = useMemo<ColumnDef<AdminUser>[]>(() => [
    { key: 'name',       accessor: (u) => u.fullName },
    { key: 'email',      accessor: (u) => u.email },
    { key: 'gender',     accessor: (u) => GENDER_LABELS[u.gender] ?? '—' },
    { key: 'nationalId', accessor: (u) => u.nationalId ?? '—' },
    // Own department only. The extra ones a user may act in are a different thing — they are
    // shown, labelled as such, in the expanded panel rather than blurred into this column.
    { key: 'department', accessor: (u) => u.orgUnitName ?? 'بلا قسم' },
    { key: 'role',       accessor: (u) => (u.roles.length ? u.roles.map(roleLabel).join('، ') : '—') },
    { key: 'clearance',  accessor: (u) => clearanceLabel(u.clearance) },
    { key: 'status',     accessor: (u) => (u.isActive ? 'مفعّل' : 'معطّل') },
  ], [])
  // Fed the searched set, not the raw one, so the column checklists offer the values that
  // are actually on screen rather than the whole directory.
  const { viewRows, getColumnProps } = useExcelTable(filtered, columns)

  // Clearance arrives as a string code; take the options from the data so a new level
  // added server-side shows up here without a second list to maintain.
  const clearances = useMemo(
    () => Array.from(new Set(users.map((u) => u.clearance))),
    [users],
  )

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <Tabs value={tab} onValueChange={(v) => setTab(v as 'list' | 'add')} dir="rtl">
        <TabsContent value="add">
      <div style={{ display: 'flex', alignItems: 'center', gap: '.75rem', marginBottom: '1rem' }}>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setTab('list')}><ArrowRight size={15} className="icon-dir" aria-hidden /> رجوع للقائمة</button>
        <h3 className="detail-h3" style={{ margin: 0 }}>إضافة مستخدم جديد</h3>
      </div>

      <form className="form-grid form-grid--4" onSubmit={createUser}>

        {/* ── Name components ── */}
        <LabeledInput label="الاسم الأول" required value={form.firstName} onChange={(e) => updateName({ firstName: e.target.value })} />
        <LabeledInput label="اسم الأب" value={form.secondName} onChange={(e) => updateName({ secondName: e.target.value })} />
        <LabeledInput label="اسم الجد" value={form.thirdName} onChange={(e) => updateName({ thirdName: e.target.value })} />
        <LabeledInput label="اسم العائلة" required value={form.familyName} onChange={(e) => updateName({ familyName: e.target.value })} />

        {/* ── Row 2: contact & identity ── */}
        {/* ── Email (auto-generated) ── */}
        <div className="space-y-1">
          {!form.emailAuto && (
            <span className="field__inline-action">
              <button
                type="button"
                className="email-auto-btn"
                onClick={resetEmailAuto}
                title="إعادة التوليد التلقائي من الاسم"
              ><RefreshCw size={14} aria-hidden /> توليد تلقائي</button>
            </span>
          )}
          <LabeledInput
            label="البريد الإلكتروني" required type="email" dir="ltr"
            value={form.email}
            onChange={(e) => setEmail(e.target.value)}
            hint={form.emailAuto && form.email ? 'تم التوليد من الاسم' : undefined}
          />
        </div>

        {/* ── National ID ── */}
        <LabeledInput
          label="رقم الهوية الوطنية" type="text" inputMode="numeric" dir="ltr" maxLength={10}
          value={form.nationalId}
          onChange={(e) => setForm((p) => ({ ...p, nationalId: e.target.value.replace(/\D/g, '') }))}
        />

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

        {/* ── Job title ── */}
        <LabeledInput label="المسمى الوظيفي" value={form.jobTitle} onChange={(e) => setForm((p) => ({ ...p, jobTitle: e.target.value }))} />

        {/* ── Row 3: credentials & access — password + confirm side by side ── */}
        <LabeledPassword label="كلمة المرور" required dir="ltr" value={form.password}
          onChange={(e) => setForm((p) => ({ ...p, password: e.target.value }))}>
          <PasswordStrengthMeter password={form.password} />
        </LabeledPassword>
        <LabeledPassword label="تأكيد كلمة المرور" required dir="ltr" value={form.confirmPassword}
          onChange={(e) => setForm((p) => ({ ...p, confirmPassword: e.target.value }))}>
          {form.confirmPassword.length > 0 && (
            <div className="pw-match-bar">
              <div className={`pw-match-bar__fill ${pwMatch ? 'pw-match--ok' : 'pw-match--err'}`} />
              <span className={`pw-match-bar__label ${pwMatch ? 'pw-match--ok' : 'pw-match--err'}`}>
                {pwMatch ? <><Check size={13} aria-hidden /> كلمتا المرور متطابقتان</> : <><X size={13} aria-hidden /> كلمتا المرور غير متطابقتين</>}
              </span>
            </div>
          )}
        </LabeledPassword>
        <LabeledSelect label="التصنيف الأمني" value={form.clearance}
          onChange={(e) => setForm((p) => ({ ...p, clearance: Number(e.target.value) as Confidentiality }))}>
          {CLEARANCE.map((c) => <option key={c.v} value={c.v}>{c.ar}</option>)}
        </LabeledSelect>
        {/* A head staffs their own department(s); the server refuses any other, and refuses
            "none" too, so offering the whole org chart only invites a rejection. With exactly
            one department to file into there is no choice left to make. */}
        {myUnits.length === 1 ? (
          <LabeledInput label="الوحدة التنظيمية" value={myUnits[0].name} readOnly
            hint="يُضاف المستخدم إلى قسمك" />
        ) : (
          <LabeledSelect label="الوحدة التنظيمية" value={form.orgUnitId}
            onChange={(e) => setForm((p) => ({ ...p, orgUnitId: e.target.value }))}>
            {canAssignAnyRole && <option value="">—</option>}
            {myUnits.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </LabeledSelect>
        )}

        {/* ── Row 4: role ──
            A head adds staff, so there is nothing to choose: the field states what the account
            will be rather than offering a menu with one item in it. */}
        {canAssignAnyRole ? (
          <LabeledSelect label="الدور" required value={form.roleId}
            onChange={(e) => setForm((p) => ({ ...p, roleId: Number(e.target.value) }))}>
            <option value={0}>—</option>
            {assignableRoles.map((r) => <option key={r.id} value={r.id}>{roleLabel(r.name)}</option>)}
          </LabeledSelect>
        ) : (
          <LabeledInput label="الدور" value={roleLabel(STAFF_ROLE)} readOnly
            hint="يضيف مدير القسم موظفين فقط — الأدوار الأخرى يمنحها مدير النظام" />
        )}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={busy}>+ إضافة مستخدم</button>
        </div>
      </form>
        </TabsContent>

        <TabsContent value="list">
      {/* ── User table ── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
        <h3 className="detail-h3" style={{ margin: 0 }}>
          {statusTab === 'active' ? 'قائمة المستخدمين' : 'المستخدمون المعطّلون'}
          <span className="users-count">
            {filtering ? `${viewRows.length} من ${inTab.length}` : `${inTab.length}`}
          </span>
        </h3>
        <button type="button" className="btn btn-primary" onClick={() => setTab('add')}>
          <span aria-hidden style={{ marginInlineEnd: 6 }}>＋</span> مستخدم جديد
        </button>
      </div>

      {/* Deactivated accounts get their own list rather than a filter value: the count carries
          on the tab, so a head can see there are dormant accounts without going looking. */}
      <Tabs value={statusTab} onValueChange={(v) => setStatusTab(v as 'active' | 'disabled')} dir="rtl">
        <TabsList className="mb-3">
          <TabsTrigger value="active">المفعّلون</TabsTrigger>
          <TabsTrigger value="disabled">
            المعطّلون
            {disabledCount > 0 && (
              <span className="badge internal" style={{ marginInlineStart: 6 }}>{disabledCount}</span>
            )}
          </TabsTrigger>
        </TabsList>
      </Tabs>

      {/* ── Search + filters ── */}
      <div className="filters users-filters">
        <div className="users-search">
          <Search size={15} className="users-search__icon" aria-hidden />
          <input
            className="filters__search"
            type="search"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="ابحث بالاسم أو البريد أو رقم الهوية أو المسمى الوظيفي…"
            aria-label="بحث في المستخدمين"
          />
        </div>
        <select className="filters__status" value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)} aria-label="تصفية حسب الدور">
          <option value="">كل الأدوار</option>
          {roles.map((r) => <option key={r.id} value={r.name}>{roleLabel(r.name)}</option>)}
        </select>
        <select className="filters__status" value={clearFilter} onChange={(e) => setClearFilter(e.target.value)} aria-label="تصفية حسب التصنيف الأمني">
          <option value="">كل التصنيفات</option>
          {clearances.map((c) => <option key={c} value={c}>{clearanceLabel(c)}</option>)}
        </select>
        {filtering && (
          <button type="button" className="btn btn-ghost btn-sm" onClick={clearFilters}>
            <X size={14} aria-hidden /> مسح التصفية
          </button>
        )}
      </div>

      <div className="table-scroll">
        <table className="reg-table users-table">
          <thead>
            <tr>
              <ExpandHeadCell />
              <th><ColumnHeaderMenu label="الاسم الكامل" {...getColumnProps('name')} /></th>
              <th><ColumnHeaderMenu label="البريد" {...getColumnProps('email')} /></th>
              <th className="col-opt3"><ColumnHeaderMenu label="الجنس" {...getColumnProps('gender')} /></th>
              <th className="col-opt2"><ColumnHeaderMenu label="رقم الهوية" {...getColumnProps('nationalId')} /></th>
              <th><ColumnHeaderMenu label="القسم" {...getColumnProps('department')} /></th>
              <th><ColumnHeaderMenu label="الدور" {...getColumnProps('role')} /></th>
              <th className="col-opt1"><ColumnHeaderMenu label="التصنيف" {...getColumnProps('clearance')} /></th>
              <th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {viewRows.length === 0 && (
              <tr><td colSpan={10} className="reg-empty">
                {/* An empty «المعطّلون» tab is good news, not a missing list — say so rather than
                    reporting it as an absence of users. */}
                {inTab.length > 0 ? 'لا نتائج مطابقة للبحث أو التصفية'
                  : statusTab === 'disabled' ? 'لا يوجد مستخدمون معطّلون'
                  : 'لا يوجد مستخدمون'}
              </td></tr>
            )}
            {viewRows.map((u) => {
              const open = isExpanded(u.id)
              return (
              <Fragment key={u.id}>
              <tr>
                <ExpandCell expanded={open} onToggle={() => {
                  toggle(u.id)
                  if (!open && !userGroups[u.id]) {
                    groupsApi.ofUser(u.id)
                      .then((g) => setUserGroups((m) => ({ ...m, [u.id]: g })))
                      .catch(() => setUserGroups((m) => ({ ...m, [u.id]: [] })))
                  }
                }} />
                {/* Clamped: one seeded account carries a 190-character name, and an
                    unclamped cell stretched the table far past the viewport — every other
                    column scrolled off and the register looked empty. Full value is in the
                    tooltip and, unclamped, in the expanded panel below. */}
                <td className="u-name" title={u.fullName}>{u.fullName}</td>
                <td className="mono u-email" dir="ltr" title={u.email}>{u.email}</td>
                <td className="col-opt3">{GENDER_LABELS[u.gender] ?? '—'}</td>
                <td className="mono col-opt2">{u.nationalId ?? '—'}</td>
                {/* Extra departments are counted, not listed: a user in charge of four would
                    otherwise stretch the column past everything beside it. The names are in the
                    panel below. */}
                <td title={[u.orgUnitName ?? 'بلا قسم', ...(u.extraOrgUnitNames ?? [])].join('، ')}>
                  {u.orgUnitName ?? <span className="muted">بلا قسم</span>}
                  {(u.extraOrgUnitNames?.length ?? 0) > 0 && (
                    <span className="badge internal" style={{ marginInlineStart: 4 }}>
                      +{u.extraOrgUnitNames!.length}
                    </span>
                  )}
                </td>
                <td>
                  {editingId === u.id ? (
                    <select
                      className="filters__status"
                      style={{ minWidth: 160 }}
                      value={editRoleId}
                      onChange={(e) => setEditRoleId(Number(e.target.value))}
                    >
                      <option value={0}>— اختر —</option>
                      {assignableRoles.map((r) => <option key={r.id} value={r.id}>{roleLabel(r.name)}</option>)}
                    </select>
                  ) : (
                    u.roles.map((r) => (
                      <span key={r} className="badge internal" style={{ marginInlineEnd: 4 }}>{roleLabel(r)}</span>
                    ))
                  )}
                </td>
                <td className="col-opt1"><span className={`badge ${clearanceClass(u.clearance)}`}>{clearanceLabel(u.clearance)}</span></td>
                <td><span className={`status-pill ${u.isActive ? 's-active' : 's-onhold'}`}>{u.isActive ? 'مفعّل' : 'معطّل'}</span></td>
                <td className="user-actions">
                  {editingId === u.id ? (
                    <>
                      <button className="btn btn-primary btn-sm" disabled={busy || !editRoleId} onClick={saveRole}>حفظ</button>{' '}
                      <button className="btn btn-ghost btn-sm" onClick={() => setEditingId(null)}>إلغاء</button>
                    </>
                  ) : (
                    <>
                      {/* Only the administrator moves anyone between roles; a head's single
                          option would be the role the person already holds. */}
                      {canAssignAnyRole && (
                        <><button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => startEdit(u)}>الدور</button>{' '}</>
                      )}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setPermTarget(u)}>الصلاحيات</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setSectionTarget(u)}>الأقسام الظاهرة</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => toggleActive(u)}>{u.isActive ? 'تعطيل' : 'تفعيل'}</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => openResetPassword(u)}>كلمة المرور</button>
                    </>
                  )}
                </td>
              </tr>
              {/* الجنس / رقم الهوية / التصنيف drop out of the row below 1500px
                  (see .col-opt* in forms.css) — the panel is where they stay reachable. */}
              <ExpandedRow colSpan={10} expanded={open}>
                <DetailFields>
                  <DetailField label="الاسم الكامل" wide><span className="u-wrap">{u.fullName}</span></DetailField>
                  <DetailField label="البريد"><span className="mono" dir="ltr">{u.email}</span></DetailField>
                  <DetailField label="الجنس">{GENDER_LABELS[u.gender] ?? '—'}</DetailField>
                  <DetailField label="رقم الهوية"><span className="mono">{u.nationalId ?? '—'}</span></DetailField>
                  <DetailField label="التصنيف"><span className={`badge ${clearanceClass(u.clearance)}`}>{clearanceLabel(u.clearance)}</span></DetailField>
                  <DetailField label="الحالة">
                    <span className={`status-pill ${u.isActive ? 's-active' : 's-onhold'}`}>{u.isActive ? 'مفعّل' : 'معطّل'}</span>
                  </DetailField>
                  <DetailField label="المجموعات" wide>
                    {userGroups[u.id] === undefined
                      ? '…'
                      : userGroups[u.id].length === 0
                        ? <span className="muted">غير مُدرج في أي مجموعة</span>
                        : userGroups[u.id].map((g) => (
                            <span key={g.id} className="badge public" style={{ marginInlineEnd: 4 }}
                              title={g.description ?? undefined}>{g.name}</span>
                          ))}
                  </DetailField>
                  <DetailField label="الأدوار" wide>
                    {u.roles.length === 0 ? '—' : u.roles.map((r) => (
                      <span key={r} className="badge internal" style={{ marginInlineEnd: 4 }}>{roleLabel(r)}</span>
                    ))}
                  </DetailField>
                  {/* Own department first and marked as such: the extra ones are what this user
                      was put in charge of, and telling them apart is the whole point. */}
                  <DetailField label="الأقسام" wide>
                    <span className="badge public" style={{ marginInlineEnd: 4 }}>{u.orgUnitName ?? 'بلا قسم'}</span>
                    {(u.extraOrgUnitNames ?? []).map((n) => (
                      <span key={n} className="badge internal" style={{ marginInlineEnd: 4 }}
                        title="قسم إضافي منحه مدير النظام">{n}</span>
                    ))}
                  </DetailField>
                </DetailFields>
                <DetailActions>
                  {canAssignAnyRole && (
                    <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => startEdit(u)}>تعديل الدور</button>
                  )}
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setPermTarget(u)}>الصلاحيات</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setSectionTarget(u)}>الأقسام الظاهرة</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => toggleActive(u)}>{u.isActive ? 'تعطيل' : 'تفعيل'}</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => openResetPassword(u)}>كلمة المرور</button>
                  {canGrantDepartments && (
                    <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => openDepartments(u)}>الأقسام</button>
                  )}
                </DetailActions>
              </ExpandedRow>
              </Fragment>
            )})}
          </tbody>
        </table>
      </div>
        </TabsContent>
      </Tabs>

      {permTarget && (
        <UserPermissionsDialog user={permTarget} onClose={() => { setPermTarget(null); void load() }} />
      )}

      {sectionTarget && (
        <UserSectionsDialog user={sectionTarget} onClose={() => setSectionTarget(null)} />
      )}

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
                  <LabeledPassword label="كلمة المرور الجديدة" required dir="ltr" autoFocus
                    value={resetPw} onChange={(e) => setResetPw(e.target.value)}>
                    <PasswordStrengthMeter password={resetPw} />
                  </LabeledPassword>
                  <LabeledPassword label="تأكيد كلمة المرور" required dir="ltr"
                    value={resetConfirmPw} onChange={(e) => setResetConfirmPw(e.target.value)}>
                    {resetConfirmPw.length > 0 && (
                      <div className="pw-match-bar">
                        <div className={`pw-match-bar__fill ${resetPwMatch ? 'pw-match--ok' : 'pw-match--err'}`} />
                        <span className={`pw-match-bar__label ${resetPwMatch ? 'pw-match--ok' : 'pw-match--err'}`}>
                          {resetPwMatch ? <><Check size={13} aria-hidden /> كلمتا المرور متطابقتان</> : <><X size={13} aria-hidden /> كلمتا المرور غير متطابقتين</>}
                        </span>
                      </div>
                    )}
                  </LabeledPassword>

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

      {/* «الأقسام» — the one control that widens what a scoped account can reach, so it is the
          system administrator's alone. The user's own department is listed but not selectable:
          it is already theirs, and unticking it here would suggest it could be taken away. */}
      {createPortal(
        <AnimatePresence>
          {deptTarget && (
            <motion.div
              className="pwdlg__backdrop"
              initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
              transition={{ duration: 0.18 }}
              onClick={() => setDeptTarget(null)}
            >
              <motion.div
                className="pwdlg"
                role="dialog" aria-modal="true" aria-label="أقسام المستخدم"
                initial={{ opacity: 0, scale: 0.95, y: 12 }}
                animate={{ opacity: 1, scale: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.95, y: 12 }}
                transition={{ duration: 0.2 }}
                onClick={(e) => e.stopPropagation()}
              >
                <h3 className="detail-h3">الأقسام التي يديرها المستخدم</h3>
                <p className="pwdlg__sub">
                  «{deptTarget.fullName}» — قسمه: {deptTarget.orgUnitName ?? 'بلا قسم'}. اختر الأقسام
                  الإضافية التي يطّلع على مراسلاتها وموظفيها وسجل حركاتها.
                </p>

                <div className="dept-grant">
                  {units.filter((o) => o.id !== deptTarget.orgUnitId).map((o) => (
                    <label key={o.id} className="dept-grant__row">
                      <input type="checkbox" checked={deptPicked.includes(o.id)}
                        onChange={(e) => setDeptPicked((p) =>
                          e.target.checked ? [...p, o.id] : p.filter((x) => x !== o.id))} />
                      <span>{o.name}</span>
                    </label>
                  ))}
                  {units.length <= 1 && <span className="muted">لا توجد أقسام أخرى</span>}
                </div>

                <div className="form-actions">
                  <button type="button" className="btn btn-primary" disabled={busy} onClick={saveDepartments}>حفظ</button>
                  <button type="button" className="btn btn-ghost" onClick={() => setDeptTarget(null)}>إلغاء</button>
                </div>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>,
        document.body,
      )}
    </motion.section>
  )
}
