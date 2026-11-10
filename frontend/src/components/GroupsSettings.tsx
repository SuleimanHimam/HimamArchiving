import { useEffect, useState, useCallback, useMemo, Fragment, type FormEvent } from 'react'
import { Pencil, Trash2, User, Building2, X } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from './ui/expandable-row'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { ColumnHeaderMenu } from './ui/column-filter'
import { groups, type Group, type GroupMemberInput } from '../lib/groups'
import { organization, type OrgUnitDto, type UserLookupDto } from '../lib/organization'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

export default function GroupsSettings() {
  const { toggle, isExpanded } = useExpandedRow<number>()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('Organization.Edit')
  const canDelete = auth.hasPermission('Organization.Delete')

  const [list, setList] = useState<Group[]>([])
  const [orgUnits, setOrgUnits] = useState<OrgUnitDto[]>([])
  const [users, setUsers] = useState<UserLookupDto[]>([])

  const [editId, setEditId] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [members, setMembers] = useState<GroupMemberInput[]>([])
  const [addUser, setAddUser] = useState('')
  const [addUnit, setAddUnit] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setList(await groups.list()) } catch { toast.error('تعذّر تحميل المجموعات') }
  }, [toast])

  useEffect(() => {
    load()
    organization.orgUnits().then(setOrgUnits).catch(() => {})
    organization.users().then(setUsers).catch(() => {})
  }, [load])

  function reset() { setEditId(null); setName(''); setDescription(''); setIsActive(true); setMembers([]); setAddUser(''); setAddUnit('') }

  function edit(g: Group) {
    setEditId(g.id); setName(g.name); setDescription(g.description ?? ''); setIsActive(g.isActive)
    setMembers(g.members.map((m) => m.memberType === 'User'
      ? { memberType: 0, userId: m.userId } : { memberType: 1, orgUnitId: m.orgUnitId }))
  }

  function addUserMember() {
    const id = Number(addUser); if (!id) return
    if (members.some((m) => m.memberType === 0 && m.userId === id)) return
    setMembers((ms) => [...ms, { memberType: 0, userId: id }]); setAddUser('')
  }
  function addUnitMember() {
    const id = Number(addUnit); if (!id) return
    if (members.some((m) => m.memberType === 1 && m.orgUnitId === id)) return
    setMembers((ms) => [...ms, { memberType: 1, orgUnitId: id }]); setAddUnit('')
  }
  const removeMember = (i: number) => setMembers((ms) => ms.filter((_, idx) => idx !== i))
  const memberLabel = (m: GroupMemberInput) => m.memberType === 0
    ? (users.find((u) => u.id === m.userId)?.fullName ?? `مستخدم #${m.userId}`)
    : (orgUnits.find((o) => o.id === m.orgUnitId)?.name ?? `قسم #${m.orgUnitId}`)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) { toast.error('اسم المجموعة مطلوب'); return }
    if (members.length === 0) { toast.error('أضف عضوًا واحدًا على الأقل'); return }
    setBusy(true)
    try {
      const body = { name: name.trim(), description: description.trim() || null, isActive, members }
      if (editId) { await groups.update(editId, body); toast.success('تم حفظ المجموعة') }
      else { await groups.create(body); toast.success('تمت إضافة المجموعة') }
      reset(); await load()
    } catch (err) { toast.error(errOf(err, 'تعذّر حفظ المجموعة')) }
    finally { setBusy(false) }
  }

  async function remove(g: Group) {
    if (!(await confirm({ message: `حذف المجموعة «${g.name}»؟`, destructive: true }))) return
    setBusy(true)
    try { await groups.remove(g.id); if (editId === g.id) reset(); await load(); toast.success('تم حذف المجموعة') }
    catch (err) { toast.error(errOf(err, 'تعذّر حذف المجموعة')) }
    finally { setBusy(false) }
  }

  const columns = useMemo<ColumnDef<Group>[]>(() => [
    { key: 'name',    accessor: (g) => g.name },
    { key: 'desc',    accessor: (g) => g.description ?? '—' },
    { key: 'members', accessor: (g) => String(g.memberCount), sortValue: (g) => g.memberCount },
    { key: 'status',  accessor: (g) => (g.isActive ? 'مفعّلة' : 'معطّلة') },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(list, columns)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">مجموعات المراسلات ({list.length})</h3>
      <p className="muted">مجموعة تضم موظفين و/أو أقسامًا. عند إرسال مراسلة داخلية إلى المجموعة تصل نسخة لكل عضو مع تتبّع القراءة.</p>

      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><ExpandHeadCell /><th><ColumnHeaderMenu label="الاسم" {...getColumnProps('name')} /></th><th><ColumnHeaderMenu label="الوصف" {...getColumnProps('desc')} /></th><th><ColumnHeaderMenu label="الأعضاء" {...getColumnProps('members')} /></th><th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>{canEdit && <th></th>}</tr></thead>
          <tbody>
            {list.length === 0 && <tr><td colSpan={6} className="reg-empty">لا توجد مجموعات</td></tr>}
            {viewRows.map((g) => {
              const open = isExpanded(g.id)
              return (
              <Fragment key={g.id}>
              <tr className={editId === g.id ? 'reg-row is-editing' : 'reg-row'}>
                <ExpandCell expanded={open} onToggle={() => toggle(g.id)} />
                <td>{g.name}</td>
                <td className="muted reg-subject">{g.description ?? '—'}</td>
                <td className="mono">{g.memberCount}</td>
                <td>{g.isActive ? <span className="badge internal">مفعّلة</span> : <span className="badge">معطّلة</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(g)}><Pencil size={14} aria-hidden /></button>
                    {canDelete && <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(g)}><Trash2 size={14} aria-hidden /></button>}
                  </td>
                )}
              </tr>
              <ExpandedRow colSpan={6} expanded={open}>
                <DetailFields>
                  <DetailField label="الاسم">{g.name}</DetailField>
                  <DetailField label="الأعضاء"><span className="mono">{g.memberCount}</span></DetailField>
                  <DetailField label="الحالة">
                    <span className={`status-pill ${g.isActive ? 's-active' : 's-onhold'}`}>{g.isActive ? 'مفعّلة' : 'معطّلة'}</span>
                  </DetailField>
                  {/* The column truncates a long description; here it wraps in full. */}
                  <DetailField label="الوصف" wide>{g.description || '—'}</DetailField>
                </DetailFields>
                {canEdit && (
                  <DetailActions>
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => edit(g)}>تعديل</button>
                    {canDelete && <button type="button" className="btn btn-ghost btn-sm text-destructive" onClick={() => remove(g)}>حذف</button>}
                  </DetailActions>
                )}
              </ExpandedRow>
              </Fragment>
            )})}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editId ? 'تعديل مجموعة' : 'إضافة مجموعة'}</h4>
          <LabeledInput label="اسم المجموعة" required value={name} onChange={(e) => setName(e.target.value)} />
          <LabeledInput label="الوصف" value={description} onChange={(e) => setDescription(e.target.value)} />
          {editId && (
            <LabeledSelect label="الحالة" value={isActive ? '1' : '0'} onChange={(e) => setIsActive(e.target.value === '1')}>
              <option value="1">مفعّلة</option><option value="0">معطّلة</option>
            </LabeledSelect>
          )}

          <div className="field field--wide"><span>الأعضاء ({members.length})</span>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, margin: '4px 0' }}>
              {members.length === 0 && <span className="muted" style={{ fontSize: 12 }}>لا أعضاء بعد.</span>}
              {members.map((m, i) => (
                <span key={i} className="badge internal" style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                  {m.memberType === 0 ? <User size={13} aria-hidden /> : <Building2 size={13} aria-hidden />} {memberLabel(m)}
                  <button type="button" onClick={() => removeMember(i)} style={{ border: 'none', background: 'none', cursor: 'pointer' }}><X size={13} aria-hidden /></button>
                </span>
              ))}
            </div>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', gap: 4 }}>
                <select value={addUser} onChange={(e) => setAddUser(e.target.value)}>
                  <option value="">— موظف —</option>
                  {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </select>
                <button type="button" className="btn btn-ghost btn-sm" onClick={addUserMember}>+ موظف</button>
              </div>
              <div style={{ display: 'flex', gap: 4 }}>
                <select value={addUnit} onChange={(e) => setAddUnit(e.target.value)}>
                  <option value="">— قسم —</option>
                  {orgUnits.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                </select>
                <button type="button" className="btn btn-ghost btn-sm" onClick={addUnitMember}>+ قسم</button>
              </div>
            </div>
          </div>

          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}
