import { useEffect, useState, useCallback, useMemo, type FormEvent } from 'react'
import { Pencil, Trash2, Users } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import {
  organization, ORG_UNIT_TYPES, ORG_UNIT_TYPE_LABEL,
  type InstitutionDto, type OrgUnitDto, type PositionDto, type UserLookupDto,
} from '../lib/organization'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { ColumnHeaderMenu } from './ui/column-filter'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

interface UnitForm { name: string; nameEn: string; code: string; type: number; sortOrder: number; parentId: number | ''; isActive: boolean }
const EMPTY_UNIT: UnitForm = { name: '', nameEn: '', code: '', type: 2, sortOrder: 0, parentId: '', isActive: true }

interface PosForm { title: string; code: string; rank: number; isActive: boolean }
const EMPTY_POS: PosForm = { title: '', code: '', rank: 0, isActive: true }

export default function OrganizationSettings() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('Organization.Edit')
  const canDelete = auth.hasPermission('Organization.Delete')

  const [institutions, setInstitutions] = useState<InstitutionDto[]>([])
  const [institutionId, setInstitutionId] = useState<number | null>(null)
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [selectedUnit, setSelectedUnit] = useState<OrgUnitDto | null>(null)
  const [positions, setPositions] = useState<PositionDto[]>([])
  const [users, setUsers] = useState<UserLookupDto[]>([])

  const [unitForm, setUnitForm] = useState<UnitForm>(EMPTY_UNIT)
  const [unitEditId, setUnitEditId] = useState<number | null>(null)
  const [posForm, setPosForm] = useState<PosForm>(EMPTY_POS)
  const [posEditId, setPosEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const loadInstitutions = useCallback(async () => {
    try {
      const list = await organization.institutions()
      setInstitutions(list)
      setInstitutionId((cur) => cur ?? list[0]?.id ?? null)
    } catch { toast.error('تعذّر تحميل المؤسسات') }
  }, [toast])

  const loadUnits = useCallback(async (instId: number | null) => {
    if (instId == null) { setUnits([]); return }
    try { setUnits(await organization.orgUnits(instId)) }
    catch { toast.error('تعذّر تحميل الوحدات التنظيمية') }
  }, [toast])

  const loadPositions = useCallback(async (unitId: number | null) => {
    if (unitId == null) { setPositions([]); return }
    try { setPositions(await organization.positions(unitId)) }
    catch { toast.error('تعذّر تحميل الموظفين') }
  }, [toast])

  useEffect(() => { loadInstitutions(); organization.users().then(setUsers).catch(() => {}) }, [loadInstitutions])
  useEffect(() => { loadUnits(institutionId) }, [institutionId, loadUnits])
  useEffect(() => { loadPositions(selectedUnit?.id ?? null) }, [selectedUnit, loadPositions])

  // ── departments (org units) ──
  const setU = <K extends keyof UnitForm>(k: K, v: UnitForm[K]) => setUnitForm((f) => ({ ...f, [k]: v }))
  function resetUnit() { setUnitForm(EMPTY_UNIT); setUnitEditId(null) }
  function editUnit(u: OrgUnitDto) {
    setUnitEditId(u.id)
    const typeNum = ORG_UNIT_TYPES.find((t) => ORG_UNIT_TYPE_LABEL[u.type] === t.ar)?.v ?? 2
    setUnitForm({ name: u.name, nameEn: u.nameEn ?? '', code: u.code ?? '', type: typeNum,
      sortOrder: u.sortOrder, parentId: u.parentId ?? '', isActive: u.isActive })
  }

  async function submitUnit(e: FormEvent) {
    e.preventDefault()
    if (institutionId == null) { toast.error('اختر المؤسسة أولاً'); return }
    if (!unitForm.name.trim()) { toast.error('اسم الوحدة مطلوب'); return }
    const base = {
      parentId: unitForm.parentId === '' ? null : Number(unitForm.parentId),
      name: unitForm.name.trim(), nameEn: unitForm.nameEn.trim() || null,
      code: unitForm.code.trim() || null, type: Number(unitForm.type), sortOrder: Number(unitForm.sortOrder) || 0,
    }
    setBusy(true)
    try {
      if (unitEditId) { await organization.updateOrgUnit(unitEditId, { ...base, isActive: unitForm.isActive }); toast.success('تم حفظ الوحدة') }
      else { await organization.createOrgUnit({ ...base, institutionId }); toast.success('تمت إضافة الوحدة') }
      resetUnit(); await loadUnits(institutionId)
    } catch (err) { toast.error(errOf(err, 'تعذّر حفظ الوحدة')) }
    finally { setBusy(false) }
  }

  async function removeUnit(u: OrgUnitDto) {
    if (!(await confirm({ message: `حذف الوحدة «${u.name}»؟`, destructive: true }))) return
    setBusy(true)
    try {
      await organization.deleteOrgUnit(u.id)
      if (unitEditId === u.id) resetUnit()
      if (selectedUnit?.id === u.id) setSelectedUnit(null)
      await loadUnits(institutionId); toast.success('تم حذف الوحدة')
    } catch (err) { toast.error(errOf(err, 'تعذّر حذف الوحدة')) }
    finally { setBusy(false) }
  }

  // ── employees (positions) ──
  const setP = <K extends keyof PosForm>(k: K, v: PosForm[K]) => setPosForm((f) => ({ ...f, [k]: v }))
  function resetPos() { setPosForm(EMPTY_POS); setPosEditId(null) }
  function editPos(p: PositionDto) {
    setPosEditId(p.id)
    setPosForm({ title: p.title, code: p.code ?? '', rank: p.rank, isActive: p.isActive })
  }

  async function submitPos(e: FormEvent) {
    e.preventDefault()
    if (!selectedUnit) { toast.error('اختر الوحدة أولاً'); return }
    if (!posForm.title.trim()) { toast.error('المسمى الوظيفي مطلوب'); return }
    const body = { title: posForm.title.trim(), code: posForm.code.trim() || null, orgUnitId: selectedUnit.id, rank: Number(posForm.rank) || 0 }
    setBusy(true)
    try {
      if (posEditId) { await organization.updatePosition(posEditId, { ...body, isActive: posForm.isActive }); toast.success('تم حفظ الموظف') }
      else { await organization.createPosition(body); toast.success('تمت إضافة الموظف') }
      resetPos(); await loadPositions(selectedUnit.id)
    } catch (err) { toast.error(errOf(err, 'تعذّر حفظ الموظف')) }
    finally { setBusy(false) }
  }

  async function removePos(p: PositionDto) {
    if (!(await confirm({ message: `حذف المنصب «${p.title}»؟`, destructive: true }))) return
    setBusy(true)
    try { await organization.deletePosition(p.id); if (posEditId === p.id) resetPos(); await loadPositions(selectedUnit?.id ?? null); toast.success('تم حذف الموظف') }
    catch (err) { toast.error(errOf(err, 'تعذّر حذف الموظف')) }
    finally { setBusy(false) }
  }

  async function assign(p: PositionDto, userId: number) {
    if (!userId) return
    setBusy(true)
    try { await organization.assignOccupant(p.id, userId); await loadPositions(selectedUnit?.id ?? null); toast.success('تم تعيين الموظف على المنصب') }
    catch (err) { toast.error(errOf(err, 'تعذّر تعيين الموظف')) }
    finally { setBusy(false) }
  }

  // Excel-style headers for the units and positions tables.
  const unitCols = useMemo<ColumnDef<OrgUnitDto>[]>(() => [
    { key: 'name',   accessor: (u) => u.name },
    { key: 'type',   accessor: (u) => ORG_UNIT_TYPE_LABEL[u.type] ?? String(u.type) },
    { key: 'parent', accessor: (u) => units.find((x) => x.id === u.parentId)?.name ?? '—' },
    { key: 'code',   accessor: (u) => u.code ?? '—' },
    { key: 'order',  accessor: (u) => String(u.sortOrder), sortValue: (u) => u.sortOrder },
    { key: 'status', accessor: (u) => (u.isActive ? 'مفعّل' : 'معطّل') },
  ], [units])
  const { viewRows: unitRows, getColumnProps: unitCol } = useExcelTable(units, unitCols)

  const posCols = useMemo<ColumnDef<PositionDto>[]>(() => [
    { key: 'title',    accessor: (p) => p.title },
    { key: 'code',     accessor: (p) => p.code ?? '—' },
    { key: 'rank',     accessor: (p) => String(p.rank), sortValue: (p) => p.rank },
    { key: 'occupant', accessor: (p) => p.currentOccupantName ?? '— شاغر —' },
    { key: 'status',   accessor: (p) => (p.isActive ? 'مفعّل' : 'معطّل') },
  ], [])
  const { viewRows: posRows, getColumnProps: posCol } = useExcelTable(positions, posCols)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">الأقسام والموظفون</h3>
      <p className="muted">إدارة الهيكل التنظيمي: الأقسام/الإدارات والمناصب (الموظفين) التابعة لها.</p>

      <div style={{ maxWidth: 320 }}>
        <LabeledSelect label="المؤسسة" value={institutionId ?? ''}
          onChange={(e) => { setInstitutionId(e.target.value ? Number(e.target.value) : null); setSelectedUnit(null) }}>
          {institutions.length === 0 && <option value="">لا توجد مؤسسات</option>}
          {institutions.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
        </LabeledSelect>
      </div>

      {/* Departments */}
      <div className="table-scroll" style={{ marginTop: '1rem' }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th><ColumnHeaderMenu label="الاسم" {...unitCol('name')} /></th>
              <th><ColumnHeaderMenu label="النوع" {...unitCol('type')} /></th>
              <th><ColumnHeaderMenu label="الوحدة الأم" {...unitCol('parent')} /></th>
              <th><ColumnHeaderMenu label="رمز" {...unitCol('code')} /></th>
              <th><ColumnHeaderMenu label="الترتيب" {...unitCol('order')} /></th>
              <th><ColumnHeaderMenu label="الحالة" {...unitCol('status')} /></th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {units.length === 0 && <tr><td colSpan={7} className="reg-empty">لا توجد وحدات</td></tr>}
            {unitRows.map((u) => (
              <tr key={u.id} className={`reg-row ${unitEditId === u.id ? 'is-editing' : ''} ${selectedUnit?.id === u.id ? 'is-selected' : ''}`}>
                {/* Class, not an inline style: `padding: 0` inline cannot be raised to a
                    finger-sized target by any stylesheet (see .ou-name in settings.css). */}
                <td><button type="button" className="ou-name" onClick={() => setSelectedUnit(u)}
                    style={{ textDecoration: selectedUnit?.id === u.id ? 'underline' : 'none' }}>{u.name}</button>
                  {u.nameEn && <span className="muted" style={{ marginInlineStart: 6 }} dir="ltr">{u.nameEn}</span>}</td>
                <td>{ORG_UNIT_TYPE_LABEL[u.type] ?? u.type}</td>
                <td>{units.find((x) => x.id === u.parentId)?.name ?? '—'}</td>
                <td className="mono">{u.code ?? '—'}</td>
                <td className="mono">{u.sortOrder}</td>
                <td>{u.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                <td className="row-actions">
                  <button className="btn btn-ghost btn-sm" title="الموظفون" onClick={() => setSelectedUnit(u)}><Users size={14} aria-hidden /></button>
                  {canEdit && <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => editUnit(u)}><Pencil size={14} aria-hidden /></button>}
                  {canDelete && <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => removeUnit(u)}><Trash2 size={14} aria-hidden /></button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submitUnit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{unitEditId ? 'تعديل وحدة' : 'إضافة وحدة / قسم'}</h4>
          <LabeledInput label="الاسم" required value={unitForm.name} onChange={(e) => setU('name', e.target.value)} />
          <LabeledInput label="الاسم بالإنجليزية" dir="ltr" value={unitForm.nameEn} onChange={(e) => setU('nameEn', e.target.value)} />
          <LabeledSelect label="النوع" value={unitForm.type} onChange={(e) => setU('type', Number(e.target.value))}>
            {ORG_UNIT_TYPES.map((t) => <option key={t.v} value={t.v}>{t.ar}</option>)}
          </LabeledSelect>
          <LabeledSelect label="الوحدة الأم" value={unitForm.parentId} onChange={(e) => setU('parentId', e.target.value === '' ? '' : Number(e.target.value))}>
            <option value="">—</option>
            {units.filter((u) => u.id !== unitEditId).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </LabeledSelect>
          <LabeledInput label="رمز (اختياري)" dir="ltr" value={unitForm.code} onChange={(e) => setU('code', e.target.value)} />
          <LabeledInput label="الترتيب" type="number" dir="ltr" value={unitForm.sortOrder} onChange={(e) => setU('sortOrder', Number(e.target.value))} />
          {unitEditId && (
            <LabeledSelect label="الحالة" value={unitForm.isActive ? '1' : '0'} onChange={(e) => setU('isActive', e.target.value === '1')}>
              <option value="1">مفعّل</option><option value="0">معطّل</option>
            </LabeledSelect>
          )}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{unitEditId ? 'حفظ' : '+ إضافة'}</button>
            {unitEditId && <button type="button" className="btn btn-ghost" onClick={resetUnit}>إلغاء</button>}
          </div>
        </form>
      )}

      {/* Employees (positions) for the selected department */}
      {selectedUnit && (
        <div style={{ marginTop: '2rem', borderTop: '1px solid var(--line, #e5e7eb)', paddingTop: '1rem' }}>
          <h3 className="detail-h3">موظفو وحدة «{selectedUnit.name}» ({positions.length})</h3>
          <p className="muted">المناصب (المقاعد الوظيفية) وشاغلوها. العمل يتبع المنصب لا الشخص.</p>

          <div className="table-scroll">
            <table className="reg-table">
              <thead>
                <tr><th><ColumnHeaderMenu label="المسمى الوظيفي" {...posCol('title')} /></th><th><ColumnHeaderMenu label="رمز" {...posCol('code')} /></th><th><ColumnHeaderMenu label="الرتبة" {...posCol('rank')} /></th><th><ColumnHeaderMenu label="الموظف الحالي" {...posCol('occupant')} /></th><th><ColumnHeaderMenu label="الحالة" {...posCol('status')} /></th><th></th></tr>
              </thead>
              <tbody>
                {positions.length === 0 && <tr><td colSpan={6} className="reg-empty">لا يوجد موظفون في هذه الوحدة</td></tr>}
                {posRows.map((p) => (
                  <tr key={p.id} className={posEditId === p.id ? 'reg-row is-editing' : 'reg-row'}>
                    <td>{p.title}</td>
                    <td className="mono">{p.code ?? '—'}</td>
                    <td className="mono">{p.rank}</td>
                    <td>
                      {canEdit ? (
                        <select value={p.currentOccupantUserId ?? ''} onChange={(e) => assign(p, Number(e.target.value))}>
                          <option value="">— شاغر —</option>
                          {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                        </select>
                      ) : (p.currentOccupantName ?? '— شاغر —')}
                    </td>
                    <td>{p.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                    <td className="row-actions">
                      {canEdit && <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => editPos(p)}><Pencil size={14} aria-hidden /></button>}
                      {canDelete && <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => removePos(p)}><Trash2 size={14} aria-hidden /></button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {canEdit && (
            <form className="form-grid" onSubmit={submitPos} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{posEditId ? 'تعديل منصب' : 'إضافة موظف / منصب'}</h4>
              <LabeledInput label="المسمى الوظيفي" required value={posForm.title} onChange={(e) => setP('title', e.target.value)} />
              <LabeledInput label="رمز (اختياري)" dir="ltr" value={posForm.code} onChange={(e) => setP('code', e.target.value)} />
              <LabeledInput label="الرتبة (أعلى = أعلى صلاحية)" type="number" dir="ltr" value={posForm.rank} onChange={(e) => setP('rank', Number(e.target.value))} />
              {posEditId && (
                <LabeledSelect label="الحالة" value={posForm.isActive ? '1' : '0'} onChange={(e) => setP('isActive', e.target.value === '1')}>
                  <option value="1">مفعّل</option><option value="0">معطّل</option>
                </LabeledSelect>
              )}
              <div className="form-actions">
                <button className="btn btn-primary" disabled={busy}>{posEditId ? 'حفظ' : '+ إضافة'}</button>
                {posEditId && <button type="button" className="btn btn-ghost" onClick={resetPos}>إلغاء</button>}
              </div>
            </form>
          )}
        </div>
      )}
    </motion.section>
  )
}
