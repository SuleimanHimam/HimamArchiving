import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { hrTime, monthDays, type RosterChange, type SaveShiftType, type ShiftTypeDto } from '../../lib/hrTime'
import { useSites } from '../../lib/hrHooks'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { Check, MonthPicker } from './hrtShared'
import { thisMonth } from '../../lib/hrtUtils'
import './hrTime.css'

/** المناوبات (slide 14): the monthly roster of a site with its coverage row, shift types and coverage minimums. */
export default function ShiftsPage() {
  const { t, i18n } = useTranslation()
  return (
    <div>
      <PageHead title={t('hrt.shifts.title')} />
      <Tabs defaultValue="roster" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="roster">{t('hrt.shifts.roster')}</TabsTrigger>
          <TabsTrigger value="types">{t('hrt.shifts.types')}</TabsTrigger>
          <TabsTrigger value="coverage">{t('hrt.shifts.coverage')}</TabsTrigger>
        </TabsList>
        <TabsContent value="roster"><Roster /></TabsContent>
        <TabsContent value="types"><ShiftTypes /></TabsContent>
        <TabsContent value="coverage"><Coverage /></TabsContent>
      </Tabs>
    </div>
  )
}

function Roster() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const sites = useSites()
  const canEdit = auth.hasPermission('HrShifts.Edit')
  const [siteId, setSiteId] = useState<number | null>(null)
  const [{ year, month }, setMonth] = useState(thisMonth())
  const [changes, setChanges] = useState<Record<string, RosterChange>>({})
  const types = useQuery({ queryKey: ['hr', 'shiftTypes', 'all'], queryFn: () => hrTime.shiftTypes(false) }).data ?? []
  const roster = useQuery({
    queryKey: ['hr', 'roster', siteId, year, month],
    queryFn: () => hrTime.roster(siteId!, year, month),
    enabled: !!siteId,
  })
  const days = useMemo(() => monthDays(year, month), [year, month])
  const cellMap = useMemo(() => new Map(roster.data?.cells.map((c) => [`${c.employeeId}|${c.date}`, c]) ?? []), [roster.data])
  const typeName = (id: number | null) => types.find((x) => x.id === id)?.code ?? '?'

  function valueOf(empId: number, date: string) {
    const key = `${empId}|${date}`
    const ch = changes[key]
    if (ch) return ch.kind === 'Clear' ? '' : ch.kind === 'Rest' ? 'R' : String(ch.shiftTypeId)
    const c = cellMap.get(key)
    return !c ? '' : c.kind === 'Rest' ? 'R' : c.kind === 'Leave' ? 'L' : String(c.shiftTypeId)
  }

  function change(empId: number, date: string, v: string) {
    const key = `${empId}|${date}`
    setChanges((x) => ({
      ...x,
      [key]: v === '' ? { employeeId: empId, date, kind: 'Clear', shiftTypeId: null }
        : v === 'R' ? { employeeId: empId, date, kind: 'Rest', shiftTypeId: null }
        : { employeeId: empId, date, kind: 'Shift', shiftTypeId: Number(v) },
    }))
  }

  async function save() {
    try {
      await hrTime.saveCells(siteId!, year, month, Object.values(changes))
      setChanges({})
      toast.success(t('hr.common.saved'))
      await qc.invalidateQueries({ queryKey: ['hr', 'roster'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function publish() {
    if (!await confirm({ message: t('hrt.shifts.confirmPublish') })) return
    try {
      await hrTime.publish(siteId!, year, month)
      toast.success(t('hrt.shifts.published'))
      await qc.invalidateQueries({ queryKey: ['hr', 'roster'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  // Coverage after the unsaved edits, so the red row reacts while planning.
  const coverage = useMemo(() => {
    const rows = new Map<string, { count: number; min: number }>()
    roster.data?.coverage.forEach((c) => rows.set(`${c.shiftTypeId}|${c.date}`, { count: c.count, min: c.minCount }))
    for (const ch of Object.values(changes)) {
      const before = cellMap.get(`${ch.employeeId}|${ch.date}`)
      if (before?.kind === 'Shift' && before.shiftTypeId) {
        const r = rows.get(`${before.shiftTypeId}|${ch.date}`); if (r) r.count--
      }
      if (ch.kind === 'Shift' && ch.shiftTypeId) {
        const k = `${ch.shiftTypeId}|${ch.date}`
        const r = rows.get(k) ?? { count: 0, min: 0 }; r.count++; rows.set(k, r)
      }
    }
    return rows
  }, [roster.data, changes, cellMap])
  const coverageTypes = [...new Set([...coverage.keys()].map((k) => Number(k.split('|')[0])))]
  const dirty = Object.keys(changes).length > 0

  return (
    <section className="doc-card">
      <div className="form-grid">
        <LabeledSelect label={t('hrt.common.site')} required value={siteId ?? ''} onChange={(e) => { setSiteId(e.target.value ? Number(e.target.value) : null); setChanges({}) }}>
          <option value="">{t('hrt.common.pickSite')}</option>
          {sites.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
        <MonthPicker year={year} month={month} onChange={(y, m) => { setMonth({ year: y, month: m }); setChanges({}) }} />
        {roster.data && (
          <div className="form-actions">
            <span className={`status-pill ${roster.data.status === 'Published' ? 's-closed' : 's-new'}`}>{t(`hrt.shifts.status.${roster.data.status}`)}</span>
            {canEdit && <button className="btn btn-primary" disabled={!dirty} onClick={save}>{t('hr.common.save')}</button>}
            {canEdit && <button className="btn btn-ghost" disabled={dirty} onClick={publish}>{t('hrt.shifts.publish')}</button>}
          </div>
        )}
      </div>
      {!siteId && <p className="muted">{t('hrt.shifts.pickSiteHint')}</p>}
      {roster.data && roster.data.employees.length === 0 && <p className="muted">{t('hrt.shifts.noStaff')}</p>}
      {roster.data && roster.data.employees.length > 0 && (
        <div className="table-scroll">
          <table className="roster-grid">
            <thead>
              <tr>
                <th className="roster-name">{t('hrt.common.employee')}</th>
                {days.map((d) => <th key={d} dir="ltr">{Number(d.slice(8))}</th>)}
              </tr>
            </thead>
            <tbody>
              {roster.data.employees.map((e) => (
                <tr key={e.id}>
                  <th className="roster-name" scope="row">{e.fullName}</th>
                  {days.map((d) => {
                    const v = valueOf(e.id, d)
                    if (v === 'L') return <td key={d} className="roster-leave" title={t('hrt.shifts.leaveLocked')}>{t('hrt.shifts.leaveShort')}</td>
                    return (
                      <td key={d} className={changes[`${e.id}|${d}`] ? 'roster-dirty' : undefined}>
                        <select aria-label={`${e.fullName} ${d}`} disabled={!canEdit} value={v} onChange={(ev) => change(e.id, d, ev.target.value)}>
                          <option value="">·</option>
                          <option value="R">{t('hrt.shifts.restShort')}</option>
                          {types.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.code}</option>)}
                        </select>
                      </td>
                    )
                  })}
                </tr>
              ))}
              {coverageTypes.map((st) => (
                <tr key={`cov-${st}`} className="roster-coverage">
                  <th className="roster-name" scope="row">{t('hrt.shifts.coverageRow', { code: typeName(st) })}</th>
                  {days.map((d) => {
                    const c = coverage.get(`${st}|${d}`)
                    const short = c && c.min > 0 && c.count < c.min
                    return <td key={d} className={short ? 'roster-short' : undefined}>{c ? `${c.count}${c.min ? `/${c.min}` : ''}` : '0'}</td>
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function ShiftTypes() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('HrShifts.Edit')
  const types = useQuery({ queryKey: ['hr', 'shiftTypes', 'all'], queryFn: () => hrTime.shiftTypes(false) })
  const empty: SaveShiftType = { code: '', name: '', startTime: '', endTime: '', graceMinutes: 0, isNight: false, isActive: true }
  const [editing, setEditing] = useState<ShiftTypeDto | null>(null)
  const [f, setF] = useState<SaveShiftType>(empty)
  const reset = () => { setEditing(null); setF(empty) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      if (editing) await hrTime.updateShiftType(editing.id, { ...f, rowVersion: editing.rowVersion })
      else await hrTime.createShiftType(f)
      toast.success(t('hr.common.saved'))
      reset()
      await qc.invalidateQueries({ queryKey: ['hr', 'shiftTypes'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hr.settings.code')}</th><th>{t('hr.settings.name')}</th><th>{t('hrt.shifts.from')}</th><th>{t('hrt.shifts.to')}</th>
            <th>{t('hrt.shifts.grace')}</th><th>{t('hrt.shifts.night')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {types.data?.length === 0 && <tr><td colSpan={8} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {types.data?.map((x) => (
              <tr key={x.id} className="reg-row">
                <td dir="ltr">{x.code}</td><td>{x.name}</td><td dir="ltr">{x.startTime}</td><td dir="ltr">{x.endTime}</td>
                <td>{x.graceMinutes}</td><td>{x.isNight ? t('hr.common.yes') : t('hr.common.no')}</td>
                <td>{x.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && <td className="row-actions"><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.edit')} title={t('hr.common.edit')}
                  onClick={() => { setEditing(x); setF({ ...x }) }}><Pencil size={14} aria-hidden /></button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editing ? t('hr.common.edit') : t('hrt.shifts.addType')}</h4>
          <LabeledInput label={t('hr.settings.code')} required dir="ltr" value={f.code} onChange={(e) => setF((x) => ({ ...x, code: e.target.value }))} />
          <LabeledInput label={t('hr.settings.name')} required value={f.name} onChange={(e) => setF((x) => ({ ...x, name: e.target.value }))} />
          <LabeledInput label={t('hrt.shifts.from')} type="time" required dir="ltr" value={f.startTime} onChange={(e) => setF((x) => ({ ...x, startTime: e.target.value }))} />
          <LabeledInput label={t('hrt.shifts.to')} type="time" required dir="ltr" value={f.endTime} onChange={(e) => setF((x) => ({ ...x, endTime: e.target.value }))} />
          <LabeledInput label={t('hrt.shifts.grace')} type="number" min={0} max={240} dir="ltr" value={f.graceMinutes}
            onChange={(e) => setF((x) => ({ ...x, graceMinutes: Number(e.target.value) }))} />
          <Check label={t('hrt.shifts.night')} checked={f.isNight} onChange={(v) => setF((x) => ({ ...x, isNight: v }))} />
          <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => setF((x) => ({ ...x, isActive: v }))} />
          <div className="form-actions">
            <button className="btn btn-primary">{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={reset}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}

function Coverage() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const sites = useSites()
  const canEdit = auth.hasPermission('HrShifts.Edit')
  const [siteId, setSiteId] = useState<number | null>(null)
  const types = useQuery({ queryKey: ['hr', 'shiftTypes', 'active'], queryFn: () => hrTime.shiftTypes(true) }).data ?? []
  const rules = useQuery({ queryKey: ['hr', 'coverage', siteId], queryFn: () => hrTime.coverage(siteId!), enabled: !!siteId }).data ?? []
  const [draft, setDraft] = useState<Record<number, string>>({})

  async function save(shiftTypeId: number) {
    try {
      await hrTime.saveCoverage({ siteId: siteId!, shiftTypeId, minCount: Number(draft[shiftTypeId] ?? 0) })
      toast.success(t('hr.common.saved'))
      await qc.invalidateQueries({ queryKey: ['hr', 'coverage'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.shifts.coverageHint')}</p>
      <div className="form-grid">
        <LabeledSelect label={t('hrt.common.site')} value={siteId ?? ''} onChange={(e) => { setSiteId(e.target.value ? Number(e.target.value) : null); setDraft({}) }}>
          <option value="">{t('hrt.common.pickSite')}</option>
          {sites.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
      </div>
      {siteId && (
        <table className="reg-table">
          <thead><tr><th>{t('hrt.shifts.types')}</th><th>{t('hrt.shifts.minCount')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {types.map((st) => {
              const current = rules.find((r) => r.shiftTypeId === st.id)?.minCount ?? 0
              return (
                <tr key={st.id} className="reg-row">
                  <td>{st.name} <span className="muted" dir="ltr">({st.startTime}–{st.endTime})</span></td>
                  <td><input aria-label={`${t('hrt.shifts.minCount')} ${st.name}`} type="number" min={0} disabled={!canEdit} dir="ltr" style={{ width: '6rem' }}
                    value={draft[st.id] ?? current} onChange={(e) => setDraft((d) => ({ ...d, [st.id]: e.target.value }))} /></td>
                  {canEdit && <td><button className="btn btn-ghost btn-sm" onClick={() => save(st.id)}>{t('hr.common.save')}</button></td>}
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </section>
  )
}
