import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Ban, CheckCheck, Play, Plus, Scale, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { day, useOrgUnits, useSites } from '../../lib/hrHooks'
import { todayHebron, useEmployeeOptions } from '../../lib/hrtUtils'
import { supply, num, COUNT_SCOPES, CONDITIONS, type CountCycleDto, type CountStatus, type SaveCountCycle } from '../../lib/supply'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import { Check } from '../hr/hrtShared'
import { BarcodeInput, CountStatusPill } from './supplyShared'
import { useWarehouses } from '../../lib/supplyHooks'

const STEPS: CountStatus[] = ['Planning', 'Counting', 'Reconciled', 'Settling', 'Closed']

/** دورات الجرد: list and planning. The counting screen is CountCyclePage. */
export default function CountsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [form, setForm] = useState(false)
  const { data, isLoading } = useQuery({ queryKey: ['supply', 'counts'], queryFn: supply.counts })
  const canPlan = auth.hasPermission('SupplyCount.Create')
  return (
    <div>
      <PageHead title={t('sup.count.title')}>
        {canPlan && !form && <button className="btn btn-primary" onClick={() => setForm(true)}><Plus size={15} aria-hidden /> {t('sup.count.new')}</button>}
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.count.subtitle')}</p>
      {form && <CycleForm existing={null} onDone={async (id) => { setForm(false); await qc.invalidateQueries({ queryKey: ['supply'] }); navigate(`/app/supply-counts/${id}`) }} onCancel={() => setForm(false)} />}
      <section className="doc-card">
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('sup.count.number')}</th><th>{t('sup.count.name')}</th><th>{t('sup.count.scope')}</th><th>{t('sup.doc.date')}</th><th>{t('sup.count.committee')}</th><th>{t('hr.common.status')}</th></tr></thead>
            <tbody>
              {isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {data?.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.map((c) => (
                <tr key={c.id} className="reg-row" onClick={() => navigate(`/app/supply-counts/${c.id}`)}>
                  <td dir="ltr" className="mono">{c.number}</td><td>{c.name}</td><td>{scopeText(t, c)}</td><td dir="ltr">{day(c.plannedDate)}</td>
                  <td>{c.committeeNames.join('، ')}</td><td><CountStatusPill status={c.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

const scopeText = (t: (k: string) => string, c: CountCycleDto) =>
  `${t(`sup.countScope.${c.scope}`)}${c.warehouseName ? `: ${c.warehouseName}` : c.orgUnitName ? `: ${c.orgUnitName}` : c.siteName ? `: ${c.siteName}` : ''}`

function CycleForm({ existing, onDone, onCancel }: { existing: CountCycleDto | null; onDone: (id: number) => Promise<void>; onCancel: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const warehouses = useWarehouses()
  const units = useOrgUnits()
  const sites = useSites()
  const employees = useEmployeeOptions()
  const [f, setF] = useState<SaveCountCycle>(existing
    ? { name: existing.name, scope: existing.scope, warehouseId: existing.warehouseId, orgUnitId: existing.orgUnitId, siteId: existing.siteId,
        plannedDate: existing.plannedDate, freezeMovements: existing.freezeMovements, committeeEmployeeIds: existing.committeeEmployeeIds, rowVersion: existing.rowVersion }
    : { name: '', scope: 'Warehouse', warehouseId: null, orgUnitId: null, siteId: null, plannedDate: todayHebron(), freezeMovements: true, committeeEmployeeIds: [] })
  const set = (p: Partial<SaveCountCycle>) => setF((x) => ({ ...x, ...p }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      const saved = await supply.saveCount(existing?.id ?? null, f)
      toast.success(t('hr.common.saved'))
      await onDone(saved.id)
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form className="doc-card form-grid" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{existing ? t('hr.common.edit') : t('sup.count.new')}</h4>
      <LabeledInput label={t('sup.count.name')} required value={f.name} onChange={(e) => set({ name: e.target.value })} />
      <LabeledInput label={t('sup.doc.date')} type="date" dir="ltr" required value={f.plannedDate} onChange={(e) => set({ plannedDate: e.target.value })} />
      <LabeledSelect label={t('sup.count.scope')} value={f.scope} onChange={(e) => set({ scope: e.target.value })}>
        {COUNT_SCOPES.map((s) => <option key={s} value={s}>{t(`sup.countScope.${s}`)}</option>)}
      </LabeledSelect>
      {f.scope === 'Warehouse' && (
        <LabeledSelect label={t('sup.req.warehouse')} required value={f.warehouseId ?? ''} onChange={(e) => set({ warehouseId: Number(e.target.value) || null })}>
          <option value="" />{warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </LabeledSelect>
      )}
      {f.scope === 'OrgUnit' && (
        <LabeledSelect label={t('sup.req.unit')} required value={f.orgUnitId ?? ''} onChange={(e) => set({ orgUnitId: Number(e.target.value) || null })}>
          <option value="" />{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
        </LabeledSelect>
      )}
      {f.scope === 'Site' && (
        <LabeledSelect label={t('sup.count.site')} required value={f.siteId ?? ''} onChange={(e) => set({ siteId: Number(e.target.value) || null })}>
          <option value="" />{sites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
      )}
      <Check label={t('sup.count.freeze')} checked={f.freezeMovements} onChange={(v) => set({ freezeMovements: v })} />
      <fieldset className="field--wide" style={{ border: '1px solid var(--line)', borderRadius: 8, padding: '.5rem .8rem' }}>
        <legend className="muted" style={{ fontSize: '.8rem' }}>{t('sup.count.committee')}</legend>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '.3rem 1rem', maxBlockSize: '10rem', overflow: 'auto' }}>
          {employees.map((e) => (
            <Check key={e.id} label={e.fullName} checked={f.committeeEmployeeIds.includes(e.id)}
              onChange={(v) => set({ committeeEmployeeIds: v ? [...f.committeeEmployeeIds, e.id] : f.committeeEmployeeIds.filter((x) => x !== e.id) })} />
          ))}
        </div>
      </fieldset>
      <div className="form-actions">
        <button className="btn btn-primary">{t('hr.common.save')}</button>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
      </div>
    </form>
  )
}

/** One count cycle (screen 18): stepper, blind entry for the committee (tablet-first), then variances and settlement. */
export function CountCyclePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const id = Number(useParams().id)
  const { data: c, refetch } = useQuery({ queryKey: ['supply', 'count', id], queryFn: () => supply.count(id) })
  const [editing, setEditing] = useState(false)
  const canPlan = auth.hasPermission('SupplyCount.Create')
  if (!c) return <p className="muted">{t('hr.common.loading')}</p>
  const step = STEPS.indexOf(c.status)

  async function act(kind: 'start' | 'reconcile' | 'settle' | 'cancel') {
    if (!await confirm({ message: t(`sup.count.confirm.${kind}`), destructive: kind === 'cancel' })) return
    try {
      if (kind === 'start') await supply.startCount(id)
      else if (kind === 'reconcile') await supply.reconcile(id)
      else if (kind === 'settle') await supply.settle(id)
      else await supply.cancelCount(id)
      toast.success(t(`sup.count.done.${kind}`))
      await refetch()
      await qc.invalidateQueries({ queryKey: ['supply', 'variances', id] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div>
      <PageHead title={`${c.name}${c.warehouseName ? ` — ${c.warehouseName}` : ''}`}>
        <button className="btn btn-ghost" onClick={() => navigate('/app/supply-counts')}><ArrowRight size={15} aria-hidden /> {t('sup.common.back')}</button>
      </PageHead>
      <p className="muted mono" dir="ltr" style={{ marginTop: '-.5rem', textAlign: 'end' }}>{c.number}</p>
      <section className="doc-card" style={{ marginBottom: '1rem' }}>
        <ol className="sup-steps">
          {STEPS.map((s, i) => (
            <li key={s} className={c.status === 'Cancelled' ? '' : i < step || c.status === 'Closed' ? 'is-done' : i === step ? 'is-current' : ''}>{t(`sup.countStep.${s}`)}</li>
          ))}
        </ol>
        {editing ? <CycleForm existing={c} onDone={async () => { setEditing(false); await refetch() }} onCancel={() => setEditing(false)} /> : (
          <dl className="sup-facts">
            <dt>{t('sup.count.scope')}</dt><dd>{scopeText(t, c)}</dd>
            <dt>{t('sup.count.committee')}</dt><dd>{c.committeeNames.join('، ') || '—'}</dd>
            <dt>{t('sup.doc.date')}</dt><dd dir="ltr">{day(c.plannedDate)}</dd>
            <dt>{t('sup.count.freeze')}</dt><dd>{c.freezeMovements ? t('sup.count.freezeOn') : t('sup.count.freezeOff')}</dd>
            <dt>{t('hr.common.status')}</dt><dd><CountStatusPill status={c.status} /></dd>
          </dl>
        )}
        {canPlan && (
          <div className="row-actions" style={{ marginTop: '.8rem' }}>
            {c.status === 'Planning' && !editing && <button className="btn btn-ghost btn-sm" onClick={() => setEditing(true)}>{t('hr.common.edit')}</button>}
            {c.status === 'Planning' && <button className="btn btn-primary btn-sm" onClick={() => act('start')}><Play size={14} aria-hidden /> {t('sup.count.start')}</button>}
            {c.status === 'Counting' && <button className="btn btn-primary btn-sm" onClick={() => act('reconcile')}><Scale size={14} aria-hidden /> {t('sup.count.reconcile')}</button>}
            {c.status === 'Reconciled' && <button className="btn btn-success btn-sm" onClick={() => act('settle')}><CheckCheck size={14} aria-hidden /> {t('sup.count.settle')}</button>}
            {(c.status === 'Planning' || c.status === 'Counting' || c.status === 'Reconciled') &&
              <button className="btn btn-ghost btn-sm" onClick={() => act('cancel')}><Ban size={14} aria-hidden /> {t('sup.count.cancel')}</button>}
            {c.adjustmentDocumentId && <button className="btn btn-ghost btn-sm" onClick={() => navigate(`/app/supply-documents?open=${c.adjustmentDocumentId}`)}>{t('sup.count.adjDoc')}</button>}
          </div>
        )}
      </section>
      {c.status === 'Counting' && (c.isCommitteeMember || canPlan) && <CountEntry cycle={c} />}
      {['Reconciled', 'Settling', 'Closed'].includes(c.status) && <Variances cycle={c} />}
    </div>
  )
}

/** Blind entry: scan barcodes, enter quantities. Nothing here shows what the records expect (standards §8.3). */
function CountEntry({ cycle }: { cycle: CountCycleDto }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const sheet = useQuery({ queryKey: ['supply', 'sheet', cycle.id], queryFn: () => supply.sheet(cycle.id) }).data ?? []
  const entries = useQuery({ queryKey: ['supply', 'entries', cycle.id], queryFn: () => supply.entries(cycle.id) })
  const [condition, setCondition] = useState('Good')
  const [qty, setQty] = useState<Record<string, string>>({})
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply', 'entries', cycle.id] })

  async function enter(body: Parameters<typeof supply.enter>[1]) {
    try {
      await supply.enter(cycle.id, body)
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  const scan = (code: string) => enter({ itemId: null, warehouseId: null, quantity: null, barcode: code, condition, note: null })
  const quantityLines = sheet.filter((l) => l.warehouseId != null)

  return (
    <>
      <section className="doc-card" style={{ marginBottom: '1rem' }}>
        <h3 className="detail-h3" style={{ marginTop: 0 }}>{t('sup.count.scanAssets')}</h3>
        <div className="sup-toolbar">
          <LabeledSelect label={t('sup.asset.condition')} value={condition} onChange={(e) => setCondition(e.target.value)}>
            {CONDITIONS.map((x) => <option key={x} value={x}>{t(`sup.condition.${x}`)}</option>)}
          </LabeledSelect>
          <div className="grow"><BarcodeInput lookup={false} onCode={(code) => void scan(code)} /></div>
        </div>
      </section>
      {quantityLines.length > 0 && (
        <section className="doc-card" style={{ marginBottom: '1rem' }}>
          <h3 className="detail-h3" style={{ marginTop: 0 }}>{t('sup.count.quantities')}</h3>
          <div className="table-scroll">
            <table className="reg-table">
              <thead><tr><th>{t('sup.item.code')}</th><th>{t('sup.item.item')}</th><th>{t('sup.req.warehouse')}</th><th>{t('sup.count.counted')}</th></tr></thead>
              <tbody>
                {quantityLines.map((l) => {
                  const key = `${l.itemId}-${l.warehouseId}`
                  return (
                    <tr key={key} className="sup-count-row">
                      <td dir="ltr" className="mono">{l.itemCode}</td><td>{l.itemName}</td><td>{l.warehouseName}</td>
                      <td style={{ display: 'flex', gap: '.4rem', alignItems: 'center' }}>
                        <input type="number" min={0} step="any" dir="ltr" className="input" aria-label={t('sup.count.counted')} value={qty[key] ?? ''}
                          onChange={(e) => setQty((q) => ({ ...q, [key]: e.target.value }))} />
                        <span>{l.unit}</span>
                        <button type="button" className="btn btn-ghost btn-sm" disabled={qty[key] == null || qty[key] === ''}
                          onClick={async () => { await enter({ itemId: l.itemId, warehouseId: l.warehouseId, quantity: Number(qty[key]), barcode: null, condition: null, note: null }); setQty((q) => ({ ...q, [key]: '' })) }}>
                          {t('sup.count.record')}</button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}
      <section className="doc-card">
        <h3 className="detail-h3" style={{ marginTop: 0 }}>{t('sup.count.entries', { count: entries.data?.length ?? 0 })}</h3>
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('sup.item.item')}</th><th>{t('sup.common.barcode')}</th><th className="num">{t('sup.count.counted')}</th><th>{t('sup.asset.condition')}</th><th aria-label={t('hr.common.actions')} /></tr></thead>
            <tbody>
              {entries.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {entries.data?.map((e) => (
                <tr key={e.id}>
                  <td>{e.itemName ?? <span className="muted">{t('sup.count.unknownLabel')}</span>}{e.assetNumber && <span className="mono" dir="ltr"> · {e.assetNumber}</span>}</td>
                  <td dir="ltr" className="mono">{e.barcode ?? '—'}</td><td className="num">{num(e.quantity)}</td>
                  <td>{e.condition ? t(`sup.condition.${e.condition}`) : '—'}</td>
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title={t('sup.common.remove')} aria-label={t('sup.common.remove')}
                      onClick={async () => { await supply.deleteEntry(cycle.id, e.id); await refresh() }}><Trash2 size={14} aria-hidden /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  )
}

function Variances({ cycle }: { cycle: CountCycleDto }) {
  const { t } = useTranslation()
  const toast = useToast()
  const { data, refetch } = useQuery({ queryKey: ['supply', 'variances', cycle.id], queryFn: () => supply.variances(cycle.id) })
  const [text, setText] = useState<Record<number, string>>({})
  const editable = cycle.status === 'Reconciled' && auth.hasPermission('SupplyCount.Create')
  const counts = (data ?? []).reduce<Record<string, number>>((m, v) => ({ ...m, [v.kind]: (m[v.kind] ?? 0) + 1 }), {})

  async function justify(vid: number) {
    try {
      await supply.justify(cycle.id, vid, text[vid] ?? '')
      await refetch()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '.5rem', alignItems: 'center' }}>
        <h3 className="detail-h3" style={{ margin: 0 }}>{t('sup.count.variances')}</h3>
        <div style={{ display: 'flex', gap: '.35rem', flexWrap: 'wrap' }}>
          {Object.entries(counts).map(([k, n]) => <span key={k} className="status-pill s-onhold">{t(`sup.variance.${k}`)}: {n}</span>)}
        </div>
      </div>
      <div className="table-scroll" style={{ marginTop: '.6rem' }}>
        <table className="reg-table">
          <thead><tr><th>{t('sup.doc.type')}</th><th>{t('sup.count.ref')}</th><th>{t('sup.doc.statement')}</th><th className="num">{t('sup.count.expected')}</th>
            <th className="num">{t('sup.count.counted')}</th><th className="num">{t('sup.count.diff')}</th><th>{t('sup.count.justification')}</th></tr></thead>
          <tbody>
            {data?.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('sup.count.noVariances')}</td></tr>}
            {data?.map((v) => (
              <tr key={v.id}>
                <td><span className={`status-pill ${v.kind === 'Missing' || v.kind === 'Shortage' ? 's-overdue' : 's-onhold'}`}>{t(`sup.variance.${v.kind}`)}</span></td>
                <td dir="ltr" className="mono">{v.assetNumber ?? v.barcode ?? '—'}</td>
                <td>{v.itemName ?? v.description}{v.warehouseName && <span className="muted"> · {v.warehouseName}</span>}
                  {v.countedCondition && <span className="muted"> → {t(`sup.condition.${v.countedCondition}`)}</span>}</td>
                <td className="num">{num(v.expected)}</td><td className="num">{num(v.counted)}</td>
                <td className="num" style={{ color: v.difference < 0 ? 'var(--seal)' : undefined }}>{num(v.difference)}</td>
                <td>
                  {editable ? (
                    <div style={{ display: 'flex', gap: '.3rem' }}>
                      <input className="input" aria-label={t('sup.count.justification')} value={text[v.id] ?? v.justification ?? ''} onChange={(e) => setText((x) => ({ ...x, [v.id]: e.target.value }))} />
                      <button className="btn btn-ghost btn-sm" onClick={() => justify(v.id)}>{t('hr.common.save')}</button>
                    </div>
                  ) : v.justification ?? '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
