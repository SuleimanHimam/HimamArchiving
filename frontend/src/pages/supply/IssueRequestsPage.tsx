import { Fragment, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CalendarClock, ClipboardCheck, PackageCheck, PauseCircle, Pencil, Plus, Send, Trash2, X } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { day } from '../../lib/hrHooks'
import { thisMonth } from '../../lib/hrtUtils'
import { supply, num, isAssetKind, type AssetDto, type IssueRequestDto, type SaveIssueRequest } from '../../lib/supply'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import { Check, EmployeeSelect } from '../hr/hrtShared'
import { BarcodeInput, Folders, KindPill, Pager, RequestStatusPill } from './supplyShared'
import { useItems, useWarehouses } from '../../lib/supplyHooks'

type Folder = 'mine' | 'Pending' | 'Submitted' | 'all'

/** طلبات الصرف (screen 15, §6.3): request → direct manager → supply manager → keeper issues. */
export default function IssueRequestsPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [params] = useSearchParams()
  const seesOthers = auth.hasPermission('SupplyRequests.View')
  const isKeeper = auth.hasPermission('SupplyRequests.Edit')
  const [folder, setFolder] = useState<Folder>(isKeeper ? 'Pending' : 'mine')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<number | null>(Number(params.get('open')) || null)
  const [form, setForm] = useState<IssueRequestDto | 'new' | null>(params.get('new') ? 'new' : null)
  const { data, isLoading, isError } = useQuery({
    queryKey: ['supply', 'requests', folder, page],
    queryFn: () => supply.requests({ mine: folder === 'mine', status: folder === 'Pending' || folder === 'Submitted' ? folder : undefined, page, pageSize: 25 }),
    placeholderData: keepPreviousData,
  })
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply'] })

  async function runQuotas() {
    const { year, month } = thisMonth()
    try {
      const r = await supply.runQuotas(year, month)
      toast.success(t('sup.req.quotasDone', r))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  const folders: { key: Folder; label: string }[] = [
    { key: 'mine', label: t('sup.req.mine') },
    ...(seesOthers ? [{ key: 'Pending' as Folder, label: t('sup.req.pending') }, { key: 'Submitted' as Folder, label: t('sup.req.inApproval') },
      { key: 'all' as Folder, label: t('sup.req.all') }] : []),
  ]

  return (
    <div>
      <PageHead title={t('sup.req.title')}>
        {isKeeper && <button className="btn btn-ghost" onClick={runQuotas}><CalendarClock size={15} aria-hidden /> {t('sup.req.runQuotas')}</button>}
        {!form && <button className="btn btn-primary" onClick={() => setForm('new')}><Plus size={15} aria-hidden /> {t('sup.req.new')}</button>}
      </PageHead>
      {form && <RequestForm key={form === 'new' ? 'new' : form.id} existing={form === 'new' ? null : form}
        onDone={async (id) => { setForm(null); setOpen(id); await refresh() }} onCancel={() => setForm(null)} />}
      {folders.length > 1 && <Folders<Folder> items={folders} value={folder} onChange={(f) => { setFolder(f); setPage(1) }} />}
      <section className="doc-card">
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr><th>{t('sup.req.number')}</th><th>{t('sup.req.requester')}</th><th>{t('sup.req.unit')}</th><th>{t('sup.req.warehouse')}</th>
                <th>{t('sup.req.items')}</th><th>{t('sup.doc.date')}</th><th>{t('hr.common.status')}</th></tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data?.items.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((r) => (
                <Fragment key={r.id}>
                  <tr className="reg-row" onClick={() => setOpen(open === r.id ? null : r.id)}>
                    <td dir="ltr" className="mono">{r.number ?? '—'}</td><td>{r.requesterName}</td>
                    <td>{r.orgUnitName}{r.forDepartment && <span className="muted"> · {t('sup.req.forDept')}</span>}</td>
                    <td>{r.warehouseName}</td><td>{r.lines.length}</td><td dir="ltr">{day(r.createdAt)}</td>
                    <td><RequestStatusPill status={r.status} /></td>
                  </tr>
                  {open === r.id && <tr><td colSpan={7}><RequestDetail r={r} onEdit={() => setForm(r)} onChanged={refresh} /></td></tr>}
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

function RequestDetail({ r, onEdit, onChanged }: { r: IssueRequestDto; onEdit: () => void; onChanged: () => Promise<unknown> }) {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const warehouses = useWarehouses()
  const mine = warehouses.some((w) => w.id === r.warehouseId && w.isMine) && auth.hasPermission('SupplyRequests.Edit')
  const waiting = r.status === 'Approved' || r.status === 'PartiallyIssued' || r.status === 'Suspended'
  const editable = r.status === 'Draft' || r.status === 'Returned'
  const [fulfil, setFulfil] = useState(false)
  const [reason, setReason] = useState('')

  async function act(kind: 'submit' | 'cancel') {
    if (!await confirm({ message: t(`sup.req.confirm.${kind}`), destructive: kind === 'cancel' })) return
    try {
      if (kind === 'submit') await supply.submitRequest(r.id)
      else await supply.cancelRequest(r.id)
      toast.success(t(`sup.req.done.${kind}`))
      await onChanged()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  async function suspend(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.suspend(r.id, reason)
      toast.success(t('sup.req.done.suspend'))
      await onChanged()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div className="sup-card" style={{ margin: '.25rem 0 .75rem' }}>
      <dl className="sup-facts">
        <dt>{t('sup.req.justification')}</dt><dd>{r.justification || '—'}</dd>
        <dt>{t('sup.req.path')}</dt><dd>{t('sup.req.pathText')}</dd>
        {r.decisionNote && <><dt>{t('sup.doc.decision')}</dt><dd>{r.decisionNote}</dd></>}
      </dl>
      <div className="table-scroll" style={{ marginTop: '.6rem' }}>
        <table className="reg-table">
          <thead><tr><th>{t('sup.item.item')}</th><th>{t('sup.item.kind')}</th><th className="num">{t('sup.req.requested')}</th><th className="num">{t('sup.req.issued')}</th><th className="num">{t('sup.req.available')}</th></tr></thead>
          <tbody>
            {r.lines.map((l) => (
              <tr key={l.id}><td>{l.itemName}</td><td><KindPill kind={l.itemKind} /></td><td className="num">{num(l.quantity)} {l.unit}</td>
                <td className="num">{num(l.issuedQuantity)}</td><td className="num">{num(l.available)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="row-actions" style={{ marginTop: '.6rem' }}>
        {editable && <button className="btn btn-ghost btn-sm" onClick={onEdit}><Pencil size={14} aria-hidden /> {t('hr.common.edit')}</button>}
        {editable && <button className="btn btn-primary btn-sm" onClick={() => act('submit')}><Send size={14} aria-hidden /> {t('sup.req.submit')}</button>}
        {(editable || (mine && waiting)) && <button className="btn btn-ghost btn-sm" onClick={() => act('cancel')}><Ban size={14} aria-hidden /> {t('sup.req.cancel')}</button>}
        {mine && waiting && !fulfil && <button className="btn btn-success btn-sm" onClick={() => setFulfil(true)}><PackageCheck size={14} aria-hidden /> {t('sup.req.fulfil')}</button>}
      </div>
      {mine && waiting && fulfil && <FulfilForm r={r} onDone={async () => { setFulfil(false); await onChanged() }} onCancel={() => setFulfil(false)} />}
      {mine && (r.status === 'Approved' || r.status === 'PartiallyIssued') && !fulfil && (
        <form className="form-grid" onSubmit={suspend} style={{ marginTop: '.6rem' }}>
          <LabeledInput label={t('sup.req.suspendReason')} required value={reason} onChange={(e) => setReason(e.target.value)} containerClassName="field--wide" />
          <div className="form-actions"><button className="btn btn-ghost btn-sm"><PauseCircle size={14} aria-hidden /> {t('sup.req.suspend')}</button></div>
        </form>
      )}
    </div>
  )
}

/** Keeper: quantities per line; asset pieces are picked by scanning their barcodes. */
function FulfilForm({ r, onDone, onCancel }: { r: IssueRequestDto; onDone: () => Promise<void>; onCancel: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const [qty, setQty] = useState<Record<number, number>>(() =>
    Object.fromEntries(r.lines.map((l) => [l.id, isAssetKind(l.itemKind) ? 0 : Math.min(Math.max(0, l.quantity - l.issuedQuantity), Math.max(0, l.available))])))
  const [picked, setPicked] = useState<Record<number, AssetDto[]>>({})
  const [receivedBy, setReceivedBy] = useState('')

  function scan(a: AssetDto) {
    const line = r.lines.find((l) => l.itemId === a.itemId && isAssetKind(l.itemKind))
    if (!line) { toast.error(t('sup.req.notInRequest')); return }
    if (a.status !== 'InStock' || a.warehouseId !== r.warehouseId) { toast.error(t('sup.req.notInWarehouse')); return }
    const list = picked[line.id] ?? []
    if (list.some((x) => x.id === a.id)) return
    if (list.length >= line.quantity - line.issuedQuantity) { toast.error(t('sup.req.enoughPieces')); return }
    setPicked((p) => ({ ...p, [line.id]: [...list, a] }))
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.fulfil(r.id, {
        lines: r.lines.map((l) => isAssetKind(l.itemKind)
          ? { lineId: l.id, quantity: (picked[l.id] ?? []).length, assetIds: (picked[l.id] ?? []).map((a) => a.id) }
          : { lineId: l.id, quantity: qty[l.id] ?? 0, assetIds: null }),
        receivedByName: receivedBy || null, rowVersion: r.rowVersion,
      })
      toast.success(t('sup.req.done.fulfil'))
      await onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form onSubmit={submit} style={{ marginTop: '.8rem', borderTop: '1px solid var(--line)', paddingTop: '.8rem' }}>
      <h4 className="detail-h3" style={{ marginTop: 0 }}><ClipboardCheck size={15} aria-hidden /> {t('sup.req.fulfilTitle')}</h4>
      {r.lines.some((l) => isAssetKind(l.itemKind)) && <BarcodeInput onAsset={scan} />}
      <div className="table-scroll" style={{ marginTop: '.5rem' }}>
        <table className="reg-table">
          <thead><tr><th>{t('sup.item.item')}</th><th className="num">{t('sup.req.remaining')}</th><th className="num">{t('sup.req.available')}</th><th>{t('sup.req.issueNow')}</th></tr></thead>
          <tbody>
            {r.lines.map((l) => (
              <tr key={l.id}>
                <td>{l.itemName}</td><td className="num">{num(l.quantity - l.issuedQuantity)}</td><td className="num">{num(l.available)}</td>
                <td>
                  {isAssetKind(l.itemKind)
                    ? <span className="mono" dir="ltr">{(picked[l.id] ?? []).map((a) => a.assetNumber).join('، ') || t('sup.req.scanPieces')}</span>
                    : <input className="input" type="number" min={0} step="any" dir="ltr" aria-label={t('sup.req.issueNow')} value={qty[l.id] ?? 0}
                        onChange={(e) => setQty((q) => ({ ...q, [l.id]: Number(e.target.value) }))} />}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="form-grid" style={{ marginTop: '.6rem' }}>
        <LabeledInput label={t('sup.doc.receivedBy')} value={receivedBy} onChange={(e) => setReceivedBy(e.target.value)} />
        <div className="form-actions">
          <button className="btn btn-success"><PackageCheck size={15} aria-hidden /> {t('sup.req.issueDoc')}</button>
          <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
        </div>
      </div>
    </form>
  )
}

/** The request form (screen 15): requester, department, path, items with what is on hand, justification. */
function RequestForm({ existing, onDone, onCancel }: { existing: IssueRequestDto | null; onDone: (id: number) => Promise<void>; onCancel: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const items = useItems(true)
  const warehouses = useWarehouses().filter((w) => w.isActive && w.kind !== 'Maintenance')
  const onBehalf = auth.hasPermission('SupplyRequests.Edit')
  const [f, setF] = useState<SaveIssueRequest>(existing
    ? { employeeId: existing.requesterEmployeeId, forDepartment: existing.forDepartment, warehouseId: existing.warehouseId, justification: existing.justification,
        lines: existing.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity })), rowVersion: existing.rowVersion }
    : { employeeId: null, forDepartment: false, warehouseId: warehouses[0]?.id ?? 0, justification: '', lines: [{ itemId: 0, quantity: 1 }] })
  const set = (p: Partial<SaveIssueRequest>) => setF((x) => ({ ...x, ...p }))
  const warehouseId = f.warehouseId || warehouses[0]?.id || 0
  const available = useQuery({ queryKey: ['supply', 'available', warehouseId], queryFn: () => supply.available(warehouseId), enabled: warehouseId > 0 }).data ?? {}

  async function submit(e: FormEvent, andSubmit: boolean) {
    e.preventDefault()
    try {
      const saved = await supply.saveRequest(existing?.id ?? null, { ...f, warehouseId, lines: f.lines.filter((l) => l.itemId) })
      if (andSubmit) await supply.submitRequest(saved.id)
      toast.success(t(andSubmit ? 'sup.req.done.submit' : 'hr.common.saved'))
      await onDone(saved.id)
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form className="doc-card" onSubmit={(e) => submit(e, true)} style={{ marginBottom: '1rem' }}>
      <div className="row-actions" style={{ justifyContent: 'space-between', borderBottom: '1px solid var(--line)', paddingBottom: '.6rem', marginBottom: '.8rem' }}>
        <h4 className="detail-h3" style={{ margin: 0 }}><ClipboardCheck size={16} aria-hidden /> {existing ? `${t('hr.common.edit')} — ${existing.number ?? ''}` : t('sup.req.formTitle')}</h4>
        <div className="row-actions">
          <button type="button" className="btn btn-ghost" onClick={onCancel}><X size={15} aria-hidden /> {t('hr.common.cancel')}</button>
          <button type="button" className="btn btn-ghost" onClick={(e) => submit(e as unknown as FormEvent, false)}>{t('sup.req.saveDraft')}</button>
          <button className="btn btn-success"><Send size={15} aria-hidden /> {t('sup.req.submit')}</button>
        </div>
      </div>
      <div className="form-grid">
        {onBehalf && !existing && <EmployeeSelect label={t('sup.req.requesterOther')} value={f.employeeId} onChange={(v) => set({ employeeId: v })} />}
        <LabeledSelect label={t('sup.req.warehouse')} required value={warehouseId || ''} onChange={(e) => set({ warehouseId: Number(e.target.value) })}>
          {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </LabeledSelect>
        <Check label={t('sup.req.forDeptLong')} checked={f.forDepartment} onChange={(v) => set({ forDepartment: v })} />
        <p className="muted field--wide" style={{ margin: 0 }}>{t('sup.req.path')}: {t('sup.req.pathText')}</p>
      </div>
      <h4 className="detail-h3">{t('sup.req.items')}</h4>
      <div className="table-scroll sup-lines">
        <table className="reg-table">
          <thead><tr><th>{t('sup.item.item')}</th><th>{t('sup.item.kind')}</th><th>{t('sup.doc.qty')}</th><th className="num">{t('sup.req.available')}</th><th aria-label={t('hr.common.actions')} /></tr></thead>
          <tbody>
            {f.lines.map((l, i) => {
              const item = items.find((x) => x.id === l.itemId)
              return (
                <tr key={i}>
                  <td>
                    <select className="input" aria-label={t('sup.item.item')} value={l.itemId || ''} onChange={(e) => set({ lines: f.lines.map((x, j) => (j === i ? { ...x, itemId: Number(e.target.value) } : x)) })}>
                      <option value="" />
                      {items.filter((x) => x.kind !== 'MaintenanceSupply').map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                    </select>
                  </td>
                  <td>{item && <KindPill kind={item.kind} />}</td>
                  <td style={{ display: 'flex', gap: '.4rem', alignItems: 'center' }}>
                    <input className="input" type="number" min={isAssetKind(item?.kind) ? 1 : 0} step={isAssetKind(item?.kind) ? 1 : 'any'} dir="ltr" aria-label={t('sup.doc.qty')}
                      value={l.quantity} onChange={(e) => set({ lines: f.lines.map((x, j) => (j === i ? { ...x, quantity: Number(e.target.value) } : x)) })} />
                    <span>{item?.unit}</span>
                  </td>
                  <td className="num">{item ? num(available[item.id] ?? 0) : '—'}</td>
                  <td className="row-actions">
                    <button type="button" className="btn btn-ghost btn-sm" title={t('sup.common.remove')} aria-label={t('sup.common.remove')}
                      onClick={() => set({ lines: f.lines.filter((_, j) => j !== i) })}><Trash2 size={14} aria-hidden /></button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <button type="button" className="btn btn-ghost" style={{ marginTop: '.5rem' }} onClick={() => set({ lines: [...f.lines, { itemId: 0, quantity: 1 }] })}>
        <Plus size={14} aria-hidden /> {t('sup.doc.addItem')}
      </button>
      <LabeledTextarea label={t('sup.req.justification')} placeholder={t('sup.req.justificationHint')} value={f.justification}
        onChange={(e) => set({ justification: e.target.value })} containerClassName="field--wide" />
    </form>
  )
}
