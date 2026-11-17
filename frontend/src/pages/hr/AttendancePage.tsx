import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { hrTime, RESOLUTIONS, type AttendanceExceptionDto, type ImportResult } from '../../lib/hrTime'
import { day, useSites } from '../../lib/hrHooks'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { MonthPicker } from './hrtShared'
import { thisMonth, todayHebron } from '../../lib/hrtUtils'
import './hrTime.css'
import '../approvals/approvals.css'

/** الحضور اليومي (slide 12, image 26): the day, the exceptions folders, import & processing, monthly close. */
export default function AttendancePage() {
  const { t, i18n } = useTranslation()
  const canEdit = auth.hasPermission('HrAttendance.Edit')
  return (
    <div>
      <PageHead title={t('hrt.att.title')} />
      <Tabs defaultValue="pending" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="day">{t('hrt.att.daily')}</TabsTrigger>
          <TabsTrigger value="pending">{t('hrt.att.pending')}</TabsTrigger>
          <TabsTrigger value="resolved">{t('hrt.att.resolved')}</TabsTrigger>
          {canEdit && <TabsTrigger value="import">{t('hrt.att.importTab')}</TabsTrigger>}
        </TabsList>
        <TabsContent value="day"><Daily /></TabsContent>
        <TabsContent value="pending"><Exceptions status="Pending" canEdit={canEdit} /></TabsContent>
        <TabsContent value="resolved"><Exceptions status="Resolved" canEdit={false} /></TabsContent>
        {canEdit && <TabsContent value="import"><ImportAndClose /></TabsContent>}
      </Tabs>
    </div>
  )
}

function Daily() {
  const { t } = useTranslation()
  const sites = useSites()
  const [date, setDate] = useState(todayHebron())
  const [siteId, setSiteId] = useState<number | null>(null)
  const rows = useQuery({ queryKey: ['hr', 'attDay', date, siteId], queryFn: () => hrTime.day(date, siteId ?? undefined) })
  return (
    <section className="doc-card">
      <div className="form-grid">
        <LabeledInput label={t('hrt.att.date')} type="date" dir="ltr" value={date} onChange={(e) => setDate(e.target.value)} />
        <LabeledSelect label={t('hrt.common.site')} value={siteId ?? ''} onChange={(e) => setSiteId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hr.common.all')}</option>
          {sites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
      </div>
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hrt.common.employee')}</th><th>{t('hrt.att.shift')}</th><th>{t('hrt.att.in')}</th><th>{t('hrt.att.out')}</th>
            <th>{t('hrt.att.source')}</th><th>{t('hr.common.status')}</th></tr></thead>
          <tbody>
            {rows.isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
            {rows.data?.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {rows.data?.map((d) => (
              <tr key={d.employeeId} className={d.state === 'Exception' ? 'reg-row is-overdue' : 'reg-row'}>
                <td>{d.employeeNumber} · {d.employeeName}</td><td>{d.shiftName ?? '—'}</td>
                <td dir="ltr">{d.firstIn ?? '—'}</td><td dir="ltr">{d.lastOut ?? '—'}</td>
                <td>{d.sources.map((s) => t(`hrt.source.${s}`)).join('، ') || '—'}</td>
                <td>{t(`hrt.dayState.${d.state}`)}{d.exceptions.map((x) => ` · ${t(`hrt.exKind.${x.kind}`)}`).join('')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function Exceptions({ status, canEdit }: { status: 'Pending' | 'Resolved'; canEdit: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { choose, prompt } = useConfirmDialog()
  const rows = useQuery({ queryKey: ['hr', 'attEx', status], queryFn: () => hrTime.exceptions({ status }) })

  async function resolve(x: AttendanceExceptionDto) {
    const options = RESOLUTIONS
      .filter((r) => r !== 'DeductLeave' || x.kind === 'Absent')
      .filter((r) => r !== 'PunchCorrected' || x.kind === 'MissingPunch' || x.kind === 'Absent')
      .map((r) => ({ key: r, label: t(`hrt.resolution.${r}`), description: t(`hrt.resolutionHint.${r}`) }))
    const resolution = await choose({ title: t('hrt.att.resolveTitle', { name: x.employeeName, date: x.date }), options })
    if (!resolution) return
    let correctionTime: string | null = null
    if (resolution === 'PunchCorrected') {
      correctionTime = await prompt({ title: t('hrt.att.correctionTime'), label: t('hrt.att.correctionTimeHint'), ltr: true, placeholder: '08:00' })
      if (!correctionTime) return
    }
    const note = await prompt({ title: t('hrt.att.justification'), label: t('hrt.att.justificationHint') })
    if (!note) return
    try {
      await hrTime.resolve(x.id, { resolution, note, correctionTime })
      toast.success(t('hrt.att.resolvedOk'))
      await qc.invalidateQueries({ queryKey: ['hr', 'attEx'] })
      await qc.invalidateQueries({ queryKey: ['hr', 'attDay'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  const cols = status === 'Pending' ? 5 : 6
  return (
    <section className="doc-card">
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hrt.common.employee')}</th><th>{t('hrt.att.date')}</th><th>{t('hrt.att.kind')}</th><th>{t('hrt.att.minutes')}</th>
            {status === 'Resolved' ? <><th>{t('hrt.att.decision')}</th><th>{t('hrt.att.justification')}</th></> : <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {rows.isLoading && <tr><td colSpan={cols} className="reg-empty">{t('hr.common.loading')}</td></tr>}
            {rows.data?.length === 0 && <tr><td colSpan={cols} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {rows.data?.map((x) => (
              <tr key={x.id} className={status === 'Pending' ? 'reg-row is-overdue' : 'reg-row'}>
                <td>{x.employeeName}</td><td dir="ltr">{day(x.date)}</td><td>{t(`hrt.exKind.${x.kind}`)}</td><td>{x.minutes || '—'}</td>
                {status === 'Resolved'
                  ? <><td>{x.resolution ? t(`hrt.resolution.${x.resolution}`) : '—'}</td><td>{x.resolutionNote}</td></>
                  : <td className="row-actions">{canEdit && (
                      <button className="btn btn-ghost btn-sm" onClick={() => resolve(x)}><CheckCircle2 size={14} aria-hidden /> {t('hrt.att.resolve')}</button>
                    )}</td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

/** CSV lines: employee number, date-time (local, e.g. 2026-10-05 08:30), optional device code. */
function parseCsv(text: string) {
  return text.split(/\r?\n/).map((l) => l.trim()).filter((l) => l && !/^#/.test(l)).map((l) => {
    const [employeeNumber = '', punchedAt = '', deviceCode = ''] = l.split(/[,;\t]/).map((s) => s.trim())
    return { employeeNumber, punchedAt: punchedAt.replace(' ', 'T'), deviceCode: deviceCode || null }
  })
}

function ImportAndClose() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const [csv, setCsv] = useState('')
  const [result, setResult] = useState<ImportResult | null>(null)
  const [range, setRange] = useState({ from: todayHebron().slice(0, 8) + '01', to: todayHebron() })
  const [{ year, month }, setMonth] = useState(thisMonth())
  const period = useQuery({ queryKey: ['hr', 'attPeriod', year, month], queryFn: () => hrTime.period(year, month) })
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr'] })

  async function doImport(e: FormEvent) {
    e.preventDefault()
    const lines = parseCsv(csv)
    if (lines.length === 0) return
    try {
      const r = await hrTime.importPunches('Import', lines)
      setResult(r)
      toast.success(t('hrt.att.imported', { count: r.imported }))
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function process(e: FormEvent) {
    e.preventDefault()
    try {
      const r = await hrTime.process(range.from, range.to)
      toast.success(t('hrt.att.processed', r))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function close() {
    if (!await confirm({ message: t('hrt.att.confirmClose', { month: `${year}-${String(month).padStart(2, '0')}` }), destructive: true })) return
    try {
      await hrTime.closePeriod(year, month)
      toast.success(t('hrt.att.closed'))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <>
      <form className="doc-card form-grid" onSubmit={doImport}>
        <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{t('hrt.att.importTitle')}</h4>
        <p className="muted field--wide">{t('hrt.att.importHint')}</p>
        <LabeledTextarea label={t('hrt.att.csv')} dir="ltr" rows={6} value={csv} onChange={(e) => setCsv(e.target.value)} containerClassName="field--wide" />
        <div className="form-actions"><button className="btn btn-primary" disabled={!csv.trim()}>{t('hrt.att.import')}</button></div>
        {result && (
          <div className="field--wide" role="status">
            {t('hrt.att.importResult', { imported: result.imported, duplicates: result.duplicates, errors: result.errors.length })}
            {result.errors.length > 0 && <ul>{result.errors.map((x) => <li key={x} style={{ color: 'var(--seal)' }}>{x}</li>)}</ul>}
          </div>
        )}
      </form>
      <form className="doc-card form-grid" onSubmit={process}>
        <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{t('hrt.att.processTitle')}</h4>
        <LabeledInput label={t('hrt.leave.from')} type="date" dir="ltr" required value={range.from} onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))} />
        <LabeledInput label={t('hrt.leave.to')} type="date" dir="ltr" required value={range.to} onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))} />
        <div className="form-actions"><button className="btn btn-primary">{t('hrt.att.process')}</button></div>
      </form>
      <section className="doc-card form-grid">
        <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{t('hrt.att.closeTitle')}</h4>
        <MonthPicker year={year} month={month} onChange={(y, m) => setMonth({ year: y, month: m })} />
        <div className="form-actions">
          {period.data?.isClosed
            ? <span className="status-pill s-archived">{t('hrt.att.isClosed')}</span>
            : <>
                <span className="muted">{t('hrt.att.pendingCount', { count: period.data?.pendingExceptions ?? 0 })}</span>
                <button className="btn btn-seal" onClick={close}>{t('hrt.att.close')}</button>
              </>}
        </div>
      </section>
    </>
  )
}
