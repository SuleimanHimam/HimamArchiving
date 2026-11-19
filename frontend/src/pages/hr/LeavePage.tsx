import { Fragment, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Pencil, Plus, Send } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { hrTime, type LeaveCheckDto, type LeaveRequestDto, type LeaveStatus, type SaveLeave } from '../../lib/hrTime'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField } from '../../components/ui/expandable-row'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { EmployeeSelect } from './hrtShared'
import { todayHebron } from '../../lib/hrtUtils'
import { day } from '../../lib/hrHooks'

const PILL: Record<LeaveStatus, string> = {
  Draft: 's-new', Submitted: 's-inprogress', Approved: 's-closed', Rejected: 's-overdue', Returned: 's-onhold', Cancelled: 's-archived',
}

export function LeaveStatusPill({ status }: { status: LeaveStatus }) {
  const { t } = useTranslation()
  return <span className={`status-pill ${PILL[status]}`}>{t(`hrt.leaveStatus.${status}`)}</span>
}

/** الإجازات والمغادرات والمهمات (slide 15): requests, the instant check, balances and their movements. */
export default function LeavePage() {
  const { t, i18n } = useTranslation()
  const canBalances = auth.hasPermission('HrLeave.Edit')
  return (
    <div>
      <PageHead title={t('hrt.leave.title')} />
      <Tabs defaultValue="requests" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="requests">{t('hrt.leave.requests')}</TabsTrigger>
          <TabsTrigger value="balances">{t('hrt.leave.balances')}</TabsTrigger>
        </TabsList>
        <TabsContent value="requests"><LeaveRequests /></TabsContent>
        <TabsContent value="balances"><Balances canEdit={canBalances} /></TabsContent>
      </Tabs>
    </div>
  )
}

function LeaveRequests() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [form, setForm] = useState<LeaveRequestDto | 'new' | null>(null)
  const canCreate = auth.hasPermission('HrLeave.Create')
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave', status, page],
    queryFn: () => hrTime.leaves({ status: status || undefined, page, pageSize: 25 }),
    placeholderData: keepPreviousData,
  })
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'leave'] })

  return (
    <section className="doc-card">
      <div className="form-grid">
        <LabeledSelect label={t('hr.common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          <option value="">{t('hr.common.all')}</option>
          {(Object.keys(PILL) as LeaveStatus[]).map((s) => <option key={s} value={s}>{t(`hrt.leaveStatus.${s}`)}</option>)}
        </LabeledSelect>
        {canCreate && !form && (
          <div className="form-actions"><button className="btn btn-primary" onClick={() => setForm('new')}><Plus size={15} aria-hidden /> {t('hrt.leave.new')}</button></div>
        )}
      </div>
      {form && (
        <LeaveForm key={form === 'new' ? 'new' : form.id} existing={form === 'new' ? null : form} onBehalf
          onDone={async () => { setForm(null); await refresh() }} onCancel={() => setForm(null)} />
      )}
      <LeaveTable rows={data?.items} loading={isLoading} failed={isError} showEmployee
        onEdit={canCreate ? (r) => setForm(r) : undefined} onChanged={refresh} />
      {data && data.totalPages > 1 && (
        <div className="row-actions" style={{ justifyContent: 'flex-end', marginTop: '.75rem' }}>
          <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>{t('hr.common.prev')}</button>
          <span className="muted">{page} / {data.totalPages}</span>
          <button className="btn btn-ghost btn-sm" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>{t('hr.common.next')}</button>
        </div>
      )}
    </section>
  )
}

export function LeaveTable({ rows, loading, failed, showEmployee, onEdit, onChanged }: {
  rows: LeaveRequestDto[] | undefined; loading: boolean; failed: boolean; showEmployee?: boolean
  onEdit?: (r: LeaveRequestDto) => void; onChanged: () => Promise<unknown>
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const { toggle, isExpanded } = useExpandedRow<number>()
  const cols = showEmployee ? 8 : 7

  async function act(r: LeaveRequestDto, kind: 'submit' | 'cancel') {
    if (!await confirm({ message: t(kind === 'submit' ? 'hrt.leave.confirmSubmit' : 'hrt.leave.confirmCancel'), destructive: kind === 'cancel' })) return
    try {
      if (kind === 'submit') await hrTime.submitLeave(r.id)
      else await hrTime.cancelLeave(r.id)
      toast.success(t(kind === 'submit' ? 'hrt.leave.submitted' : 'hrt.leave.cancelled'))
      await onChanged()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div className="table-scroll">
      <table className="reg-table">
        <thead>
          <tr>
            <ExpandHeadCell />
            <th>{t('hrt.leave.number')}</th><th>{t('hrt.leave.type')}</th>{showEmployee && <th>{t('hrt.common.employee')}</th>}
            <th>{t('hrt.leave.period')}</th><th>{t('hrt.leave.quantity')}</th><th>{t('hr.common.status')}</th><th aria-label={t('hr.common.actions')} />
          </tr>
        </thead>
        <tbody>
          {loading && <tr><td colSpan={cols} className="reg-empty">{t('hr.common.loading')}</td></tr>}
          {failed && <tr><td colSpan={cols} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
          {rows && rows.length === 0 && <tr><td colSpan={cols} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {rows?.map((r) => {
            const editable = r.status === 'Draft' || r.status === 'Returned'
            return (
              <Fragment key={r.id}>
                <tr className="reg-row">
                  <ExpandCell expanded={isExpanded(r.id)} onToggle={() => toggle(r.id)} />
                  <td dir="ltr">{r.number ?? '—'}</td>
                  <td>{r.leaveTypeName}</td>
                  {showEmployee && <td>{r.employeeName}</td>}
                  <td dir="ltr">{r.fromDate === r.toDate ? day(r.fromDate) : `${day(r.fromDate)} → ${day(r.toDate)}`}{r.fromTime ? ` ${r.fromTime}–${r.toTime}` : ''}</td>
                  <td>{r.quantity} {t(`hrt.unit.${r.unit}`)}</td>
                  <td><LeaveStatusPill status={r.status} /></td>
                  <td className="row-actions">
                    {editable && onEdit && (
                      <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')} onClick={() => onEdit(r)}>
                        <Pencil size={14} aria-hidden /></button>
                    )}
                    {editable && (
                      <>
                        <button className="btn btn-ghost btn-sm" title={t('hrt.leave.submit')} aria-label={t('hrt.leave.submit')} onClick={() => act(r, 'submit')}>
                          <Send size={14} aria-hidden /></button>
                        <button className="btn btn-ghost btn-sm" title={t('hrt.leave.cancel')} aria-label={t('hrt.leave.cancel')} onClick={() => act(r, 'cancel')}>
                          <Ban size={14} aria-hidden /></button>
                      </>
                    )}
                  </td>
                </tr>
                <ExpandedRow colSpan={cols} expanded={isExpanded(r.id)}>
                  <DetailFields>
                    <DetailField label={t('hrt.leave.reason')} wide>{r.reason || '—'}</DetailField>
                    {r.substituteName && <DetailField label={t('hrt.leave.substitute')}>{r.substituteName}</DetailField>}
                    {r.decisionNote && <DetailField label={t('hrt.leave.decisionNote')} wide>{r.decisionNote}</DetailField>}
                  </DetailFields>
                </ExpandedRow>
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

/** The request form with the instant check (فحص فوري). onBehalf shows the employee picker; otherwise it is the caller's own. */
export function LeaveForm({ existing, onBehalf, onDone, onCancel }: {
  existing: LeaveRequestDto | null; onBehalf?: boolean; onDone: () => Promise<void> | void; onCancel: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const types = useQuery({ queryKey: ['hr', 'leaveTypes', 'active'], queryFn: () => hrTime.leaveTypes(true) }).data ?? []
  const [f, setF] = useState<SaveLeave>(existing
    ? { employeeId: existing.employeeId, leaveTypeId: existing.leaveTypeId, fromDate: existing.fromDate, toDate: existing.toDate,
        fromTime: existing.fromTime, toTime: existing.toTime, substituteEmployeeId: existing.substituteEmployeeId, reason: existing.reason,
        rowVersion: existing.rowVersion }
    : { employeeId: null, leaveTypeId: 0, fromDate: todayHebron(), toDate: todayHebron(), fromTime: null, toTime: null, substituteEmployeeId: null, reason: '' })
  const [busy, setBusy] = useState(false)
  const type = types.find((x) => x.id === f.leaveTypeId)
  const departure = type?.kind === 'Departure'
  const set = (patch: Partial<SaveLeave>) => setF((x) => ({ ...x, ...patch }))

  // Instant check (فحص فوري): re-asked of the server whenever the inputs change.
  const checkBody = { ...f, toDate: departure ? f.fromDate : f.toDate }
  const check: LeaveCheckDto | null = useQuery({
    queryKey: ['hr', 'leaveCheck', existing?.id ?? 0, checkBody],
    queryFn: () => hrTime.checkLeave(checkBody, existing?.id),
    enabled: !!f.leaveTypeId && !(onBehalf && !f.employeeId && !existing),
    retry: false,
    placeholderData: keepPreviousData,
  }).data ?? null

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    const body = { ...f, toDate: departure ? f.fromDate : f.toDate, fromTime: departure ? f.fromTime : null, toTime: departure ? f.toTime : null }
    try {
      if (existing) await hrTime.updateLeave(existing.id, body)
      else await hrTime.createLeave(body)
      toast.success(t('hr.common.saved'))
      await onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  const blocked = !!check && check.problems.length > 0

  return (
    <form className="doc-card form-grid" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{existing ? t('hr.common.edit') : t('hrt.leave.new')}</h4>
      {onBehalf && !existing && (
        <EmployeeSelect label={t('hrt.common.employee')} required value={f.employeeId} onChange={(v) => set({ employeeId: v })} />
      )}
      <LabeledSelect label={t('hrt.leave.type')} required value={f.leaveTypeId || ''} onChange={(e) => set({ leaveTypeId: Number(e.target.value) })}>
        <option value="">{t('hrt.leave.pickType')}</option>
        {types.map((x) => <option key={x.id} value={x.id}>{x.name} ({t(`hrt.kind.${x.kind}`)})</option>)}
      </LabeledSelect>
      <LabeledInput label={departure ? t('hrt.leave.date') : t('hrt.leave.from')} type="date" dir="ltr" required value={f.fromDate}
        onChange={(e) => set({ fromDate: e.target.value })} />
      {departure ? (
        <>
          <LabeledInput label={t('hrt.leave.fromTime')} type="time" dir="ltr" required value={f.fromTime ?? ''} onChange={(e) => set({ fromTime: e.target.value })} />
          <LabeledInput label={t('hrt.leave.toTime')} type="time" dir="ltr" required value={f.toTime ?? ''} onChange={(e) => set({ toTime: e.target.value })} />
        </>
      ) : (
        <LabeledInput label={t('hrt.leave.to')} type="date" dir="ltr" required value={f.toDate} onChange={(e) => set({ toDate: e.target.value })} />
      )}
      <EmployeeSelect label={type?.requiresSubstitute ? `${t('hrt.leave.substitute')} *` : t('hrt.leave.substitute')}
        value={f.substituteEmployeeId} onChange={(v) => set({ substituteEmployeeId: v })} exclude={f.employeeId} />
      <LabeledTextarea label={t('hrt.leave.reason')} value={f.reason} onChange={(e) => set({ reason: e.target.value })} containerClassName="field--wide" />

      {check && (
        <div className="field--wide" role="status" aria-live="polite"
          style={{ border: '1px solid var(--line)', borderRadius: 8, padding: '.6rem .8rem', display: 'grid', gap: '.25rem' }}>
          <strong>{t('hrt.leave.instantCheck')}</strong>
          <span>{t('hrt.leave.quantity')}: {check.quantity} {type ? t(`hrt.unit.${type.unit}`) : ''}</span>
          {check.balance != null && <span>{t('hrt.leave.balance')}: {check.balance} → {t('hrt.leave.after')}: {check.balanceAfter}</span>}
          {check.problems.map((p) => <span key={p} style={{ color: 'var(--seal)' }}>• {p}</span>)}
          {!blocked && <span style={{ color: 'var(--ink)' }}>{t('hrt.leave.checkOk')}</span>}
        </div>
      )}

      <div className="form-actions">
        <button className="btn btn-primary" disabled={busy || blocked || !f.leaveTypeId}>{t('hr.common.save')}</button>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
      </div>
    </form>
  )
}

function Balances({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const [employeeId, setEmployeeId] = useState<number | null>(null)
  const [year, setYear] = useState(new Date().getFullYear())
  const types = useQuery({ queryKey: ['hr', 'leaveTypes', 'active'], queryFn: () => hrTime.leaveTypes(true) }).data ?? []
  const balances = useQuery({ queryKey: ['hr', 'leaveBalances', employeeId, year], queryFn: () => hrTime.balances(employeeId!, year), enabled: !!employeeId })
  const moves = useQuery({ queryKey: ['hr', 'leaveMoves', employeeId, year], queryFn: () => hrTime.movements(employeeId!, year), enabled: !!employeeId })
  const [adj, setAdj] = useState({ leaveTypeId: 0, quantity: '', kind: 'Adjustment' as 'Opening' | 'Adjustment', note: '' })
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr'] })

  async function grant() {
    if (!await confirm({ message: t('hrt.leave.confirmGrant', { year }) })) return
    try {
      const r = await hrTime.grantYear(year)
      toast.success(t('hrt.leave.granted', r))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function adjust(e: FormEvent) {
    e.preventDefault()
    if (!employeeId) return
    try {
      await hrTime.adjust({ employeeId, leaveTypeId: adj.leaveTypeId, year, quantity: Number(adj.quantity), kind: adj.kind, note: adj.note })
      toast.success(t('hr.common.saved'))
      setAdj((a) => ({ ...a, quantity: '', note: '' }))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <div className="form-grid">
        <EmployeeSelect label={t('hrt.common.employee')} value={employeeId} onChange={setEmployeeId} />
        <LabeledInput label={t('hrt.common.year')} type="number" dir="ltr" value={year} min={2000} max={2100}
          onChange={(e) => setYear(Number(e.target.value) || year)} />
        {canEdit && <div className="form-actions"><button type="button" className="btn btn-ghost" onClick={grant}>{t('hrt.leave.grantYear')}</button></div>}
      </div>
      {employeeId && (
        <>
          <BalanceTable rows={balances.data} loading={balances.isLoading} />
          <h4 className="detail-h3">{t('hrt.leave.movements')}</h4>
          <div className="table-scroll">
            <table className="reg-table">
              <thead><tr><th>{t('hrt.leave.type')}</th><th>{t('hrt.leave.movementKind')}</th><th>{t('hrt.leave.quantity')}</th><th>{t('hr.common.notes')}</th><th>{t('hrt.common.at')}</th></tr></thead>
              <tbody>
                {moves.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
                {moves.data?.map((m) => (
                  <tr key={m.id} className="reg-row">
                    <td>{m.leaveTypeName}</td><td>{t(`hrt.movement.${m.kind}`)}</td>
                    <td dir="ltr" style={{ color: m.quantity < 0 ? 'var(--seal)' : undefined }}>{m.quantity}</td>
                    <td>{m.note ?? '—'}</td><td dir="ltr">{day(m.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {canEdit && (
            <form className="form-grid" onSubmit={adjust} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{t('hrt.leave.adjust')}</h4>
              <LabeledSelect label={t('hrt.leave.type')} required value={adj.leaveTypeId || ''} onChange={(e) => setAdj((a) => ({ ...a, leaveTypeId: Number(e.target.value) }))}>
                <option value="" />
                {types.filter((x) => x.deductsBalance).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              </LabeledSelect>
              <LabeledSelect label={t('hrt.leave.movementKind')} value={adj.kind} onChange={(e) => setAdj((a) => ({ ...a, kind: e.target.value as 'Opening' | 'Adjustment' }))}>
                <option value="Opening">{t('hrt.movement.Opening')}</option>
                <option value="Adjustment">{t('hrt.movement.Adjustment')}</option>
              </LabeledSelect>
              <LabeledInput label={t('hrt.leave.quantity')} type="number" step="0.5" dir="ltr" required value={adj.quantity}
                onChange={(e) => setAdj((a) => ({ ...a, quantity: e.target.value }))} />
              <LabeledInput label={t('hrt.leave.adjustReason')} required value={adj.note} onChange={(e) => setAdj((a) => ({ ...a, note: e.target.value }))} containerClassName="field--wide" />
              <div className="form-actions"><button className="btn btn-primary">{t('hr.common.save')}</button></div>
            </form>
          )}
        </>
      )}
    </section>
  )
}

export function BalanceTable({ rows, loading }: { rows: import('../../lib/hrTime').LeaveBalanceDto[] | undefined; loading: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="table-scroll">
      <table className="reg-table">
        <thead><tr><th>{t('hrt.leave.type')}</th><th>{t('hrt.leave.entitled')}</th><th>{t('hrt.leave.taken')}</th><th>{t('hrt.leave.pending')}</th><th>{t('hrt.leave.balance')}</th></tr></thead>
        <tbody>
          {loading && <tr><td colSpan={5} className="reg-empty">{t('hr.common.loading')}</td></tr>}
          {rows?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hrt.leave.noBalances')}</td></tr>}
          {rows?.map((b) => (
            <tr key={b.leaveTypeId} className="reg-row">
              <td>{b.leaveTypeName}</td><td>{b.entitled}</td><td>{b.taken}</td><td>{b.pending}</td>
              <td><strong>{b.balance}</strong> {t(`hrt.unit.${b.unit}`)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
