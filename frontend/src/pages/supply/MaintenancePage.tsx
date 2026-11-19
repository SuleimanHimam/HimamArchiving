import { Fragment, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, PackageMinus, Wrench, X } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { day, useSites } from '../../lib/hrHooks'
import { supply, money, type WorkOrderDto } from '../../lib/supply'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { PageHead } from '../hr/hrShared'
import { Check, EmployeeSelect } from '../hr/hrtShared'
import { Folders, Pager, WorkOrderStatusPill } from './supplyShared'
import { useItems, useWarehouses } from '../../lib/supplyHooks'

type Folder = 'open' | 'mine' | 'closed'

/** الصيانة (screen 19, §7.2): from the fault report to closing, with supplies issued on the work order. */
export default function MaintenancePage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [folder, setFolder] = useState<Folder>('open')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<number | null>(null)
  const [report, setReport] = useState(false)
  const counts = useQuery({ queryKey: ['supply', 'woCounts'], queryFn: supply.workOrderCounts }).data
  const { data, isLoading, isError } = useQuery({
    queryKey: ['supply', 'workOrders', folder, search, page],
    queryFn: () => supply.workOrders({ folder, search: search || undefined, page, pageSize: 25 }),
    placeholderData: keepPreviousData,
  })
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply'] })

  return (
    <div>
      <PageHead title={t('sup.wo.title')}>
        {!report && <button className="btn btn-primary" onClick={() => setReport(true)}><Wrench size={15} aria-hidden /> {t('sup.wo.report')}</button>}
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.wo.subtitle')}</p>
      {report && <ReportForm onDone={async () => { setReport(false); await refresh() }} onCancel={() => setReport(false)} />}
      <Folders<Folder> value={folder} onChange={(f) => { setFolder(f); setPage(1) }}
        items={[{ key: 'open', label: t('sup.wo.open'), count: counts?.open }, { key: 'mine', label: t('sup.wo.mine'), count: counts?.mine },
          { key: 'closed', label: t('sup.wo.closed'), count: counts?.closed }]} />
      <section className="doc-card">
        <div className="sup-toolbar">
          <LabeledInput label={t('hr.common.search')} value={search} containerClassName="grow" onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
        </div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('sup.wo.number')}</th><th>{t('sup.doc.date')}</th><th>{t('sup.wo.fault')}</th><th>{t('sup.wo.assetOrPlace')}</th><th>{t('sup.wo.reporter')}</th><th>{t('hr.common.status')}</th></tr></thead>
            <tbody>
              {isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data?.items.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((w) => (
                <Fragment key={w.id}>
                  <tr className="reg-row" onClick={() => setOpen(open === w.id ? null : w.id)}>
                    <td dir="ltr" className="mono">{w.number}</td><td dir="ltr">{day(w.createdAt)}</td><td>{w.description}</td>
                    <td>{w.assetNumber ? <span className="mono" dir="ltr">{w.assetNumber}</span> : w.location}</td><td>{w.reporterName ?? '—'}</td>
                    <td><WorkOrderStatusPill status={w.status} /></td>
                  </tr>
                  {open === w.id && <tr><td colSpan={6}><WorkOrderDetail w={w} onChanged={refresh} /></td></tr>}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {data && <Pager page={page} totalPages={data.totalPages} onPage={setPage} />}
      </section>
    </div>
  )
}

function ReportForm({ onDone, onCancel }: { onDone: () => Promise<void>; onCancel: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const sites = useSites()
  const [f, setF] = useState({ barcode: '', location: '', siteId: null as number | null, description: '' })
  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      const w = await supply.reportFault({ barcode: f.barcode || null, location: f.location || null, siteId: f.siteId, description: f.description })
      toast.success(t('sup.wo.reported', { number: w.number }))
      await onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  return (
    <form className="doc-card form-grid" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{t('sup.wo.report')}</h4>
      <LabeledInput label={t('sup.wo.scanAsset')} dir="ltr" data-scan value={f.barcode} onChange={(e) => setF({ ...f, barcode: e.target.value })} />
      <LabeledInput label={t('sup.wo.orPlace')} value={f.location} onChange={(e) => setF({ ...f, location: e.target.value })} />
      <LabeledSelect label={t('sup.count.site')} value={f.siteId ?? ''} onChange={(e) => setF({ ...f, siteId: Number(e.target.value) || null })}>
        <option value="" />{sites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
      </LabeledSelect>
      <LabeledTextarea label={t('sup.wo.fault')} required value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} containerClassName="field--wide" />
      <div className="form-actions">
        <button className="btn btn-primary">{t('sup.wo.send')}</button>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
      </div>
    </form>
  )
}

function WorkOrderDetail({ w, onChanged }: { w: WorkOrderDto; onChanged: () => Promise<unknown> }) {
  const { t } = useTranslation()
  const toast = useToast()
  const canManage = auth.hasPermission('SupplyMaintenance.Edit')
  const open = w.status === 'New' || w.status === 'InProgress' || w.status === 'AwaitingParts'
  const parts = useItems(true).filter((i) => i.kind !== 'FixedAsset')
  const stores = useWarehouses().filter((x) => x.kind === 'Maintenance' && x.isActive)
  const [m, setM] = useState({ assignee: w.assigneeEmployeeId, status: w.status as string, assessment: w.assessment ?? '' })
  const [part, setPart] = useState({ warehouseId: stores[0]?.id ?? 0, itemId: 0, quantity: 1 })
  const [close, setClose] = useState({ repaired: true, laborHours: '', laborCost: '0', outcome: '' })

  async function run(fn: () => Promise<unknown>, done: string) {
    try { await fn(); toast.success(t(done)); await onChanged() } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div className="sup-card" style={{ margin: '.25rem 0 .75rem' }}>
      {w.suggestReplacement && <p className="status-pill s-overdue" style={{ display: 'inline-block' }}>{t('sup.asset.suggestReplacement')}</p>}
      <dl className="sup-facts">
        {w.itemName && <><dt>{t('sup.item.item')}</dt><dd>{w.itemName}</dd></>}
        <dt>{t('sup.wo.assignee')}</dt><dd>{w.assigneeName ?? '—'}</dd>
        <dt>{t('sup.wo.assessment')}</dt><dd>{w.assessment ?? '—'}</dd>
        <dt>{t('sup.wo.cost')}</dt><dd>{money(w.laborCost)} + {t('sup.wo.parts')} {money(w.partsCost)}</dd>
        {w.outcome && <><dt>{t('sup.wo.outcome')}</dt><dd>{w.outcome}</dd></>}
      </dl>
      {canManage && open && (
        <>
          <form className="form-grid" style={{ marginTop: '.8rem' }} onSubmit={(e) => { e.preventDefault(); void run(() => supply.updateWorkOrder(w.id, { assigneeEmployeeId: m.assignee, status: m.status, assessment: m.assessment || null, rowVersion: w.rowVersion }), 'hr.common.saved') }}>
            <EmployeeSelect label={t('sup.wo.assignee')} value={m.assignee} onChange={(v) => setM({ ...m, assignee: v })} />
            <LabeledSelect label={t('hr.common.status')} value={m.status} onChange={(e) => setM({ ...m, status: e.target.value })}>
              {['New', 'InProgress', 'AwaitingParts'].map((s) => <option key={s} value={s}>{t(`sup.woStatus.${s}`)}</option>)}
            </LabeledSelect>
            <LabeledInput label={t('sup.wo.assessment')} value={m.assessment} onChange={(e) => setM({ ...m, assessment: e.target.value })} containerClassName="field--wide" />
            <div className="form-actions"><button className="btn btn-ghost btn-sm">{t('hr.common.save')}</button></div>
          </form>
          <form className="form-grid" style={{ marginTop: '.8rem' }} onSubmit={(e) => { e.preventDefault(); void run(() => supply.issueParts(w.id, { warehouseId: part.warehouseId, lines: [{ itemId: part.itemId, quantity: part.quantity }], rowVersion: w.rowVersion }), 'sup.wo.partsIssued') }}>
            <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}><PackageMinus size={15} aria-hidden /> {t('sup.wo.issueParts')}</h4>
            <LabeledSelect label={t('sup.req.warehouse')} value={part.warehouseId || ''} onChange={(e) => setPart({ ...part, warehouseId: Number(e.target.value) })}>
              {stores.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </LabeledSelect>
            <LabeledSelect label={t('sup.item.item')} required value={part.itemId || ''} onChange={(e) => setPart({ ...part, itemId: Number(e.target.value) })}>
              <option value="" />{parts.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
            </LabeledSelect>
            <LabeledInput label={t('sup.doc.qty')} type="number" min={0} step="any" dir="ltr" value={part.quantity} onChange={(e) => setPart({ ...part, quantity: Number(e.target.value) })} />
            <div className="form-actions"><button className="btn btn-ghost btn-sm" disabled={!part.itemId || !part.warehouseId}>{t('sup.wo.issue')}</button></div>
          </form>
          <form className="form-grid" style={{ marginTop: '.8rem' }} onSubmit={(e) => { e.preventDefault(); void run(() => supply.closeWorkOrder(w.id, { repaired: close.repaired, laborHours: close.laborHours === '' ? null : Number(close.laborHours), laborCost: Number(close.laborCost), outcome: close.outcome, rowVersion: w.rowVersion }), 'sup.wo.closedDone') }}>
            <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}><CheckCircle2 size={15} aria-hidden /> {t('sup.wo.close')}</h4>
            <Check label={t('sup.wo.repaired')} checked={close.repaired} onChange={(v) => setClose({ ...close, repaired: v })} />
            <LabeledInput label={t('sup.wo.hours')} type="number" min={0} step="0.25" dir="ltr" value={close.laborHours} onChange={(e) => setClose({ ...close, laborHours: e.target.value })} />
            <LabeledInput label={t('sup.wo.laborCost')} type="number" min={0} step="0.01" dir="ltr" value={close.laborCost} onChange={(e) => setClose({ ...close, laborCost: e.target.value })} />
            <LabeledInput label={t('sup.wo.outcome')} required value={close.outcome} onChange={(e) => setClose({ ...close, outcome: e.target.value })} containerClassName="field--wide" />
            {!close.repaired && <p className="muted field--wide" style={{ margin: 0 }}><X size={13} aria-hidden /> {t('sup.wo.disposalHint')}</p>}
            <div className="form-actions"><button className="btn btn-success btn-sm">{t('sup.wo.close')}</button></div>
          </form>
        </>
      )}
    </div>
  )
}
