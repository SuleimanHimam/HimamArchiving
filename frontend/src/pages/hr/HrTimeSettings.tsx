import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { workflow } from '../../lib/workflow'
import { hrTime, LEAVE_KINDS, type LeaveTypeDto, type SaveLeaveType } from '../../lib/hrTime'
import { day } from '../../lib/hrHooks'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { CategoryChecks, Check } from './hrtShared'

const WEEK = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const

/** إعداد أنواع الإجازات: every entitlement, cap and flag is data the HR administrator sets. */
export function LeaveTypesSettings() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('HrLeave.Edit')
  const types = useQuery({ queryKey: ['hr', 'leaveTypes', 'all'], queryFn: () => hrTime.leaveTypes(false) })
  const workflows = useQuery({ queryKey: ['workflow', 'definitions'], queryFn: workflow.definitions, enabled: canEdit }).data ?? []
  const empty: SaveLeaveType = {
    code: '', name: '', kind: 'Leave', unit: 'Days', deductsBalance: true, countWorkingDaysOnly: true, requiresSubstitute: false,
    annualEntitlement: 0, maxCarryOver: null, appliesToMilitary: true, appliesToCivil: true, appliesToContract: true,
    workflowDefinitionId: null, isActive: true,
  }
  const [editing, setEditing] = useState<LeaveTypeDto | null>(null)
  const [f, setF] = useState<SaveLeaveType>(empty)
  const set = (p: Partial<SaveLeaveType>) => setF((x) => ({ ...x, ...p }))
  const reset = () => { setEditing(null); setF(empty) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      if (editing) await hrTime.updateLeaveType(editing.id, { ...f, rowVersion: editing.rowVersion })
      else await hrTime.createLeaveType(f)
      toast.success(t('hr.common.saved'))
      reset()
      await qc.invalidateQueries({ queryKey: ['hr', 'leaveTypes'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.leaveSet.hint')}</p>
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hr.settings.code')}</th><th>{t('hr.settings.name')}</th><th>{t('hrt.leaveSet.kind')}</th><th>{t('hrt.leaveSet.entitlement')}</th>
            <th>{t('hrt.leaveSet.carry')}</th><th>{t('hr.settings.workflow')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {types.data?.length === 0 && <tr><td colSpan={8} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {types.data?.map((x) => (
              <tr key={x.id} className="reg-row">
                <td dir="ltr">{x.code}</td><td>{x.name}</td><td>{t(`hrt.kind.${x.kind}`)}</td>
                <td>{x.deductsBalance ? `${x.annualEntitlement} ${t(`hrt.unit.${x.unit}`)}` : t('hrt.leaveSet.noBalance')}</td>
                <td>{x.maxCarryOver ?? t('hrt.leaveSet.unlimited')}</td>
                <td>{x.workflowDefinitionName ?? <span className="status-pill s-onhold">{t('hr.actions.noPath')}</span>}</td>
                <td>{x.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && <td><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.edit')} title={t('hr.common.edit')}
                  onClick={() => { setEditing(x); setF({ ...x }) }}><Pencil size={14} aria-hidden /></button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editing ? t('hr.common.edit') : t('hrt.leaveSet.add')}</h4>
          <LabeledInput label={t('hr.settings.code')} required dir="ltr" value={f.code} onChange={(e) => set({ code: e.target.value })} />
          <LabeledInput label={t('hr.settings.name')} required value={f.name} onChange={(e) => set({ name: e.target.value })} />
          <LabeledSelect label={t('hrt.leaveSet.kind')} value={f.kind}
            onChange={(e) => { const kind = e.target.value as SaveLeaveType['kind']; set({ kind, unit: kind === 'Departure' ? 'Hours' : 'Days', deductsBalance: kind !== 'Mission' && f.deductsBalance }) }}>
            {LEAVE_KINDS.map((k) => <option key={k} value={k}>{t(`hrt.kind.${k}`)}</option>)}
          </LabeledSelect>
          <LabeledInput label={`${t('hrt.leaveSet.entitlement')} (${t(`hrt.unit.${f.unit}`)})`} type="number" step="0.5" min={0} dir="ltr"
            value={f.annualEntitlement} onChange={(e) => set({ annualEntitlement: Number(e.target.value) })} />
          <LabeledInput label={t('hrt.leaveSet.carry')} type="number" step="0.5" min={0} dir="ltr" placeholder={t('hrt.leaveSet.unlimited')}
            value={f.maxCarryOver ?? ''} onChange={(e) => set({ maxCarryOver: e.target.value === '' ? null : Number(e.target.value) })} />
          <LabeledSelect label={t('hr.settings.workflow')} value={f.workflowDefinitionId ?? ''}
            onChange={(e) => set({ workflowDefinitionId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">{t('hr.actions.noPath')}</option>
            {workflows.filter((w) => w.isActive && w.triggerModule === 'ApprovalRequest').map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </LabeledSelect>
          {f.kind !== 'Mission' && <Check label={t('hrt.leaveSet.deducts')} checked={f.deductsBalance} onChange={(v) => set({ deductsBalance: v })} />}
          {f.kind !== 'Departure' && <Check label={t('hrt.leaveSet.workingDays')} checked={f.countWorkingDaysOnly} onChange={(v) => set({ countWorkingDaysOnly: v })} />}
          <Check label={t('hrt.leaveSet.substitute')} checked={f.requiresSubstitute} onChange={(v) => set({ requiresSubstitute: v })} />
          <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => set({ isActive: v })} />
          <CategoryChecks value={f} onChange={set} />
          <div className="form-actions">
            <button className="btn btn-primary">{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={reset}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}

/** الأسبوع والعطل: weekend days, default shift, leave type for absences, and the holiday calendar. */
export function WorkCalendarSettings() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('HrAttendance.Edit')
  const settings = useQuery({ queryKey: ['hr', 'calendarSettings'], queryFn: hrTime.settings })
  const shifts = useQuery({ queryKey: ['hr', 'shiftTypes', 'active'], queryFn: () => hrTime.shiftTypes(true) }).data ?? []
  const leaveTypes = (useQuery({ queryKey: ['hr', 'leaveTypes', 'active'], queryFn: () => hrTime.leaveTypes(true) }).data ?? []).filter((x) => x.kind === 'Leave')
  const [year, setYear] = useState(new Date().getFullYear())
  const holidays = useQuery({ queryKey: ['hr', 'holidays', year], queryFn: () => hrTime.holidays(year) })
  const loaded = settings.data
  const [weekend, setWeekend] = useState<string[] | null>(null)
  const [defaultShift, setDefaultShift] = useState<number | null | undefined>(undefined)
  const [absenceType, setAbsenceType] = useState<number | null | undefined>(undefined)
  // Edits win; until the user touches a field it shows what the server holds.
  const weekendNow = weekend ?? (loaded?.weekendDays ? loaded.weekendDays.split(',') : [])
  const defaultShiftNow = defaultShift === undefined ? loaded?.defaultShiftTypeId ?? null : defaultShift
  const absenceTypeNow = absenceType === undefined ? loaded?.absenceLeaveTypeId ?? null : absenceType
  const [h, setH] = useState({ date: '', name: '' })

  async function saveSettings(e: FormEvent) {
    e.preventDefault()
    try {
      await hrTime.saveSettings({ weekendDays: weekendNow.join(','), defaultShiftTypeId: defaultShiftNow, absenceLeaveTypeId: absenceTypeNow })
      toast.success(t('hr.common.saved'))
      await qc.invalidateQueries({ queryKey: ['hr', 'calendarSettings'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function addHoliday(e: FormEvent) {
    e.preventDefault()
    try {
      await hrTime.addHoliday(h)
      setH({ date: '', name: '' })
      await qc.invalidateQueries({ queryKey: ['hr', 'holidays'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function removeHoliday(id: number) {
    if (!await confirm({ message: t('hr.common.confirmDelete'), destructive: true })) return
    try { await hrTime.deleteHoliday(id); await qc.invalidateQueries({ queryKey: ['hr', 'holidays'] }) }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <>
      <form className="doc-card form-grid" onSubmit={saveSettings}>
        <p className="muted field--wide">{t('hrt.cal.hint')}</p>
        <fieldset className="field--wide" style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem', border: 0, padding: 0 }}>
          <legend className="muted" style={{ fontSize: '.8rem' }}>{t('hrt.cal.weekend')}</legend>
          {WEEK.map((d) => (
            <Check key={d} label={t(`hrt.week.${d}`)} checked={weekendNow.includes(d)}
              onChange={(v) => setWeekend(v ? [...weekendNow, d] : weekendNow.filter((x) => x !== d))} />
          ))}
        </fieldset>
        <LabeledSelect label={t('hrt.cal.defaultShift')} value={defaultShiftNow ?? ''} onChange={(e) => setDefaultShift(e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hrt.cal.none')}</option>
          {shifts.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.startTime}–{s.endTime})</option>)}
        </LabeledSelect>
        <LabeledSelect label={t('hrt.cal.absenceType')} value={absenceTypeNow ?? ''} onChange={(e) => setAbsenceType(e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hrt.cal.none')}</option>
          {leaveTypes.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
        {canEdit && <div className="form-actions"><button className="btn btn-primary">{t('hr.common.save')}</button></div>}
      </form>

      <section className="doc-card">
        <h4 className="detail-h3">{t('hrt.cal.holidays')}</h4>
        <div className="form-grid">
          <LabeledInput label={t('hrt.common.year')} type="number" dir="ltr" value={year} onChange={(e) => setYear(Number(e.target.value) || year)} />
        </div>
        <table className="reg-table">
          <thead><tr><th>{t('hrt.att.date')}</th><th>{t('hr.settings.name')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {holidays.data?.length === 0 && <tr><td colSpan={3} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {holidays.data?.map((x) => (
              <tr key={x.id} className="reg-row"><td dir="ltr">{day(x.date)}</td><td>{x.name}</td>
                {canEdit && <td><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.delete')} title={t('hr.common.delete')}
                  onClick={() => removeHoliday(x.id)}><Trash2 size={14} aria-hidden /></button></td>}</tr>
            ))}
          </tbody>
        </table>
        {canEdit && (
          <form className="form-grid" onSubmit={addHoliday} style={{ marginTop: '1rem' }}>
            <LabeledInput label={t('hrt.att.date')} type="date" dir="ltr" required value={h.date} onChange={(e) => setH((x) => ({ ...x, date: e.target.value }))} />
            <LabeledInput label={t('hr.settings.name')} required value={h.name} onChange={(e) => setH((x) => ({ ...x, name: e.target.value }))} />
            <div className="form-actions"><button className="btn btn-primary">{t('hr.common.add')}</button></div>
          </form>
        )}
      </section>
    </>
  )
}
