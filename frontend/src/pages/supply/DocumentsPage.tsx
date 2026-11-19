import { Fragment, useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Eye, PackageCheck, Pencil, Plus, Printer, Send, Trash2, X } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { day, useOrgUnits } from '../../lib/hrHooks'
import { todayHebron } from '../../lib/hrtUtils'
import {
  supply, num, money, isAssetKind, CONDITIONS, DOC_TYPES, MANUAL_DOC_TYPES,
  type AssetDto, type DocType, type DocumentDto, type SaveDocLine, type SaveDocument,
} from '../../lib/supply'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import { EmployeeSelect } from '../hr/hrtShared'
import { BarcodeInput, DocStatusPill, Folders, KindPill, Pager } from './supplyShared'
import { useItems, useWarehouses } from '../../lib/supplyHooks'

type Folder = 'all' | DocType

/** المحاضر (screen 16): every stock or custody change is a numbered document; no balance is edited directly. */
export default function DocumentsPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [folder, setFolder] = useState<Folder>('all')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<number | null>(Number(params.get('open')) || null)
  const newType = params.get('new') as DocType | null
  const [form, setForm] = useState<DocumentDto | DocType | null>(newType && MANUAL_DOC_TYPES.includes(newType) ? newType : null)
  const canCreate = auth.hasPermission('SupplyDocuments.Create')
  const counts = useQuery({ queryKey: ['supply', 'docCounts'], queryFn: supply.documentCounts }).data
  const { data, isLoading, isError } = useQuery({
    queryKey: ['supply', 'documents', folder, search, page],
    queryFn: () => supply.documents({ type: folder === 'all' ? undefined : folder, search: search || undefined, page, pageSize: 25 }),
    placeholderData: keepPreviousData,
  })
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply'] })
  const closeForm = () => { setForm(null); if (params.has('new')) { params.delete('new'); params.delete('asset'); setParams(params, { replace: true }) } }

  return (
    <div>
      <PageHead title={t('sup.doc.title')}>
        {canCreate && !form && (
          <LabeledSelect label={t('sup.doc.new')} value="" onChange={(e) => e.target.value && setForm(e.target.value as DocType)}>
            <option value="" />
            {MANUAL_DOC_TYPES.map((d) => <option key={d} value={d}>{t(`sup.docType.${d}`)}</option>)}
          </LabeledSelect>
        )}
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.doc.subtitle')}</p>

      {form && (
        <DocumentForm key={typeof form === 'string' ? form : form.id} existing={typeof form === 'string' ? null : form}
          type={typeof form === 'string' ? form : form.type} presetAssetId={Number(params.get('asset')) || null}
          onDone={async (id) => { closeForm(); setOpen(id); await refresh() }} onCancel={closeForm} />
      )}

      <Folders<Folder> value={folder} onChange={(f) => { setFolder(f); setPage(1) }}
        items={[{ key: 'all', label: t('sup.doc.all'), count: counts?.total },
          ...DOC_TYPES.map((d) => ({ key: d as Folder, label: t(`sup.docType.${d}`), count: counts?.byType[d] ?? 0 }))]} />

      <section className="doc-card">
        <div className="sup-toolbar">
          <LabeledInput label={t('hr.common.search')} value={search} containerClassName="grow" onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
          <span className="muted">{data ? `${data.items.length} / ${data.totalCount}` : ''}</span>
        </div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <th>{t('sup.doc.type')}</th><th>{t('sup.doc.number')}</th><th>{t('sup.doc.date')}</th><th>{t('sup.doc.statement')}</th>
                <th>{t('sup.doc.by')}</th><th>{t('hr.common.status')}</th><th aria-label={t('hr.common.actions')} />
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data?.items.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((d) => (
                <Fragment key={d.id}>
                  <tr className="reg-row" onClick={() => setOpen(open === d.id ? null : d.id)}>
                    <td><span className="status-pill s-new">{t(`sup.docType.${d.type}`)}</span></td>
                    <td dir="ltr" className="mono">{d.number ?? '—'}</td>
                    <td dir="ltr">{day(d.docDate)}</td>
                    <td>{d.title}</td>
                    <td>{d.createdByName ?? '—'}</td>
                    <td><DocStatusPill status={d.status} /></td>
                    <td className="row-actions" onClick={(e) => e.stopPropagation()}>
                      <button className="btn btn-ghost btn-sm" title={t('sup.common.view')} aria-label={t('sup.common.view')} onClick={() => setOpen(d.id)}><Eye size={14} aria-hidden /></button>
                      <button className="btn btn-ghost btn-sm" title={t('sup.common.print')} aria-label={t('sup.common.print')}
                        onClick={() => { setOpen(d.id); void supply.documentPrinted(d.id); setTimeout(() => window.print(), 300) }}><Printer size={14} aria-hidden /></button>
                    </td>
                  </tr>
                  {open === d.id && (
                    <tr><td colSpan={7}><DocumentDetail id={d.id} onEdit={(x) => setForm(x)} onChanged={refresh} /></td></tr>
                  )}
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

/** Details pattern: number and title, actions, info card, lines, decision. */
export function DocumentDetail({ id, onEdit, onChanged }: { id: number; onEdit?: (d: DocumentDto) => void; onChanged: () => Promise<unknown> }) {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const { data: d, refetch } = useQuery({ queryKey: ['supply', 'document', id], queryFn: () => supply.document(id) })
  const warehouses = useWarehouses()
  if (!d) return <p className="muted">{t('hr.common.loading')}</p>
  const editable = (d.status === 'Draft' || d.status === 'Returned') && auth.hasPermission('SupplyDocuments.Create')
  const canReceive = d.status === 'AwaitingReceipt' && auth.hasPermission('SupplyDocuments.Edit')
    && warehouses.some((w) => w.id === d.targetWarehouseId && w.isMine)

  async function act(kind: 'submit' | 'cancel' | 'receive') {
    if (!await confirm({ message: t(`sup.doc.confirm.${kind}`), destructive: kind === 'cancel' })) return
    try {
      if (kind === 'submit') await supply.submitDocument(id)
      else if (kind === 'cancel') await supply.cancelDocument(id)
      else await supply.confirmReceipt(id)
      toast.success(t(`sup.doc.done.${kind}`))
      await refetch()
      await onChanged()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div className="sup-card" style={{ margin: '.25rem 0 .75rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '.5rem', alignItems: 'center' }}>
        <h3 style={{ margin: 0 }}><span className="mono" dir="ltr">{d.number ?? t('sup.doc.draft')}</span> — {d.title}</h3>
        <div className="row-actions sup-no-print">
          {editable && onEdit && <button className="btn btn-ghost btn-sm" onClick={() => onEdit(d)}><Pencil size={14} aria-hidden /> {t('hr.common.edit')}</button>}
          {editable && <button className="btn btn-primary btn-sm" onClick={() => act('submit')}><Send size={14} aria-hidden /> {t('sup.doc.submit')}</button>}
          {editable && <button className="btn btn-ghost btn-sm" onClick={() => act('cancel')}><Ban size={14} aria-hidden /> {t('sup.doc.cancel')}</button>}
          {canReceive && <button className="btn btn-primary btn-sm" onClick={() => act('receive')}><PackageCheck size={14} aria-hidden /> {t('sup.doc.confirmReceipt')}</button>}
        </div>
      </div>
      <div style={{ display: 'flex', gap: '.4rem', margin: '.6rem 0' }}>
        <span className="status-pill s-new">{t(`sup.docType.${d.type}`)}</span><DocStatusPill status={d.status} />
      </div>
      <dl className="sup-facts">
        <dt>{t('sup.doc.date')}</dt><dd dir="ltr">{day(d.docDate)}</dd>
        {d.warehouseName && <><dt>{d.type === 'RCV' || d.type === 'RET' ? t('sup.doc.toWarehouse') : t('sup.doc.fromWarehouse')}</dt><dd>{d.warehouseName}</dd></>}
        {d.targetWarehouseName && <><dt>{t('sup.doc.toWarehouse')}</dt><dd>{d.targetWarehouseName}</dd></>}
        {d.recipientName && <><dt>{t('sup.doc.recipient')}</dt><dd>{d.recipientName}</dd></>}
        {d.supplier && <><dt>{t('sup.doc.supplier')}</dt><dd>{d.supplier}{d.invoiceNumber && ` — ${t('sup.doc.invoice')} ${d.invoiceNumber}`}</dd></>}
        {d.receivedByName && <><dt>{t('sup.doc.receivedBy')}</dt><dd>{d.receivedByName}</dd></>}
        <dt>{t('sup.doc.by')}</dt><dd>{d.createdByName ?? '—'}</dd>
        {d.executedAt && <><dt>{t('sup.doc.executedAt')}</dt><dd dir="ltr">{day(d.executedAt)}</dd></>}
        {d.notes && <><dt>{t('hr.common.notes')}</dt><dd>{d.notes}</dd></>}
        {d.decisionNote && <><dt>{t('sup.doc.decision')}</dt><dd>{d.decisionNote}</dd></>}
      </dl>
      <div className="table-scroll" style={{ marginTop: '.75rem' }}>
        <table className="reg-table">
          <thead><tr><th>{t('sup.item.item')}</th><th>{t('sup.item.kind')}</th><th className="num">{t('sup.doc.qty')}</th><th>{t('sup.asset.number')}</th><th>{t('sup.asset.serial')}</th><th>{t('sup.asset.condition')}</th><th className="num">{t('sup.asset.value')}</th></tr></thead>
          <tbody>
            {d.lines.map((l) => (
              <tr key={l.id}>
                <td>{l.itemName}</td><td><KindPill kind={l.itemKind} /></td>
                <td className="num" style={{ color: l.quantity < 0 ? 'var(--seal)' : undefined }}>{num(l.quantity)} {l.unit}</td>
                <td dir="ltr" className="mono">{l.assetNumber ?? '—'}</td><td dir="ltr">{l.serialNumber ?? '—'}</td>
                <td>{l.condition ? t(`sup.condition.${l.condition}`) : '—'}</td><td className="num">{money(l.unitValue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {/* Paper copy: the recipient signs here (§6.3 — or on the tablet, then scanned and attached). */}
      <div className="sup-print-only sup-print-sign">
        <div>{t('sup.doc.signKeeper')}: ____________</div><div>{t('sup.doc.signRecipient')}: ____________</div>
      </div>
    </div>
  )
}

interface LineDraft extends SaveDocLine { key: number; label?: string }
const blankLine = (key: number, itemId = 0): LineDraft => ({
  key, itemId, quantity: 1, assetId: null, serialNumber: null, barcode: null, condition: null, unitValue: null, purchaseDate: null,
  warehouseId: null, holderEmployeeId: null, holderOrgUnitId: null, note: null,
})

/** The form pattern: fixed bar (exit, add in green), then fields, then the lines. */
function DocumentForm({ existing, type, presetAssetId, onDone, onCancel }: {
  existing: DocumentDto | null; type: DocType; presetAssetId: number | null; onDone: (id: number) => Promise<void>; onCancel: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const items = useItems(type !== 'DSP' && type !== 'RET')
  const warehouses = useWarehouses().filter((w) => w.isActive)
  const units = useOrgUnits()
  const [f, setF] = useState<Omit<SaveDocument, 'lines'>>(existing
    ? { type, docDate: existing.docDate, title: existing.title, notes: existing.notes, warehouseId: existing.warehouseId, targetWarehouseId: existing.targetWarehouseId,
        recipientEmployeeId: existing.recipientEmployeeId, recipientOrgUnitId: existing.recipientOrgUnitId, supplier: existing.supplier,
        invoiceNumber: existing.invoiceNumber, receivedByName: existing.receivedByName, rowVersion: existing.rowVersion }
    : { type, docDate: todayHebron(), title: '', notes: null, warehouseId: warehouses.find((w) => w.isMine)?.id ?? null, targetWarehouseId: null,
        recipientEmployeeId: null, recipientOrgUnitId: null, supplier: null, invoiceNumber: null, receivedByName: null })
  const [lines, setLines] = useState<LineDraft[]>(existing
    ? existing.lines.map((l, i) => ({ ...l, key: i, label: l.assetNumber ?? undefined }))
    : [])
  const [seq, setSeq] = useState(1000)
  const [busy, setBusy] = useState(false)
  const set = (p: Partial<typeof f>) => setF((x) => ({ ...x, ...p }))
  const piece = type !== 'RCV'   // every type but receipt moves existing pieces by barcode

  // Disposal straight from an asset card: the piece is preloaded.
  useEffect(() => {
    if (!presetAssetId || existing) return
    supply.assetCard(presetAssetId).then((c) => addAsset(c.asset)).catch(() => undefined)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [presetAssetId])

  function addAsset(a: AssetDto) {
    if (lines.some((l) => l.assetId === a.id)) return
    setLines((ls) => [...ls, { ...blankLine(seq, a.itemId), assetId: a.id, label: `${a.assetNumber} · ${a.itemName}` }])
    setSeq((s) => s + 1)
  }
  const patch = (key: number, p: Partial<LineDraft>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...p } : l)))
  const kindOf = (itemId: number) => items.find((i) => i.id === itemId)?.kind

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      const saved = await supply.saveDocument(existing?.id ?? null, {
        ...f, lines: lines.map((l): SaveDocLine => ({
          itemId: l.itemId, quantity: l.quantity, assetId: l.assetId, serialNumber: l.serialNumber, barcode: l.barcode, condition: l.condition,
          unitValue: l.unitValue, purchaseDate: l.purchaseDate, warehouseId: l.warehouseId, holderEmployeeId: l.holderEmployeeId,
          holderOrgUnitId: l.holderOrgUnitId, note: l.note,
        })),
      })
      toast.success(t('hr.common.saved'))
      await onDone(saved.id)
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  const needsWarehouse = type !== 'TRF'
  const needsRecipient = type === 'ISS' || type === 'TRF'

  return (
    <form className="doc-card" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <div className="row-actions" style={{ justifyContent: 'space-between', borderBottom: '1px solid var(--line)', paddingBottom: '.6rem', marginBottom: '.8rem' }}>
        <h4 className="detail-h3" style={{ margin: 0 }}>{existing ? `${t('hr.common.edit')} — ${existing.number ?? ''}` : t(`sup.doc.new${type}`, { defaultValue: t(`sup.docType.${type}`) })}</h4>
        <div className="row-actions">
          <button type="button" className="btn btn-ghost" onClick={onCancel}><X size={15} aria-hidden /> {t('hr.common.cancel')}</button>
          <button className="btn btn-success" disabled={busy || lines.length === 0}><Plus size={15} aria-hidden /> {t('hr.common.save')}</button>
        </div>
      </div>
      <div className="form-grid">
        <LabeledInput label={t('sup.doc.statement')} required value={f.title} onChange={(e) => set({ title: e.target.value })} containerClassName="field--wide" />
        <LabeledInput label={t('sup.doc.date')} type="date" dir="ltr" required value={f.docDate} onChange={(e) => set({ docDate: e.target.value })} />
        {needsWarehouse && (
          <LabeledSelect label={type === 'RCV' || type === 'RET' ? t('sup.doc.toWarehouse') : t('sup.doc.fromWarehouse')} required value={f.warehouseId ?? ''}
            onChange={(e) => set({ warehouseId: e.target.value ? Number(e.target.value) : null })}>
            <option value="" />
            {warehouses.filter((w) => w.isMine).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </LabeledSelect>
        )}
        {type === 'MOV' && (
          <LabeledSelect label={t('sup.doc.toWarehouse')} required value={f.targetWarehouseId ?? ''} onChange={(e) => set({ targetWarehouseId: e.target.value ? Number(e.target.value) : null })}>
            <option value="" />
            {warehouses.filter((w) => w.id !== f.warehouseId).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </LabeledSelect>
        )}
        {type === 'RCV' && (
          <>
            <LabeledInput label={t('sup.doc.supplier')} required value={f.supplier ?? ''} onChange={(e) => set({ supplier: e.target.value })} />
            <LabeledInput label={t('sup.doc.invoice')} dir="ltr" value={f.invoiceNumber ?? ''} onChange={(e) => set({ invoiceNumber: e.target.value })} />
          </>
        )}
        {needsRecipient && (
          <>
            <EmployeeSelect label={t('sup.doc.recipientEmployee')} value={f.recipientEmployeeId} onChange={(v) => set({ recipientEmployeeId: v, recipientOrgUnitId: type === 'TRF' && v ? null : f.recipientOrgUnitId })} />
            <LabeledSelect label={t('sup.doc.recipientUnit')} value={f.recipientOrgUnitId ?? ''}
              onChange={(e) => set({ recipientOrgUnitId: e.target.value ? Number(e.target.value) : null, recipientEmployeeId: type === 'TRF' && e.target.value ? null : f.recipientEmployeeId })}>
              <option value="" />
              {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </LabeledSelect>
          </>
        )}
        {(type === 'ISS' || type === 'RET') && (
          <LabeledInput label={t('sup.doc.receivedBy')} value={f.receivedByName ?? ''} onChange={(e) => set({ receivedByName: e.target.value })} />
        )}
        <LabeledTextarea label={t('hr.common.notes')} value={f.notes ?? ''} onChange={(e) => set({ notes: e.target.value })} containerClassName="field--wide" />
      </div>

      <h4 className="detail-h3">{t('sup.doc.lines')}</h4>
      {piece && <BarcodeInput onAsset={addAsset} />}
      <div className="table-scroll sup-lines" style={{ marginTop: '.5rem' }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('sup.item.item')}</th><th className="num">{t('sup.doc.qty')}</th>
              {type === 'RCV' && <><th>{t('sup.asset.serial')}</th><th>{t('sup.asset.value')}</th></>}
              {type === 'RET' && <th>{t('sup.asset.condition')}</th>}
              <th aria-label={t('hr.common.actions')} />
            </tr>
          </thead>
          <tbody>
            {lines.length === 0 && <tr><td colSpan={5} className="reg-empty">{piece ? t('sup.doc.scanOrAdd') : t('sup.doc.addLines')}</td></tr>}
            {lines.map((l) => {
              const asset = isAssetKind(kindOf(l.itemId))
              return (
                <tr key={l.key}>
                  <td>
                    {l.assetId ? <span>{l.label}</span> : (
                      <select className="input" aria-label={t('sup.item.item')} value={l.itemId || ''} onChange={(e) => patch(l.key, { itemId: Number(e.target.value) })}>
                        <option value="" />
                        {items.filter((i) => type === 'RCV' || !isAssetKind(i.kind)).map((i) => <option key={i.id} value={i.id}>{i.code} · {i.name}</option>)}
                      </select>
                    )}
                  </td>
                  <td className="num">
                    {asset ? '1' : <input className="input" type="number" min={0} step="any" dir="ltr" aria-label={t('sup.doc.qty')} value={l.quantity}
                      onChange={(e) => patch(l.key, { quantity: Number(e.target.value) })} />}
                  </td>
                  {type === 'RCV' && (
                    <>
                      <td>{asset ? <input className="input" dir="ltr" aria-label={t('sup.asset.serial')} value={l.serialNumber ?? ''} onChange={(e) => patch(l.key, { serialNumber: e.target.value || null })} /> : '—'}</td>
                      <td><input className="input" type="number" min={0} step="0.01" dir="ltr" aria-label={t('sup.asset.value')} value={l.unitValue ?? ''}
                        onChange={(e) => patch(l.key, { unitValue: e.target.value === '' ? null : Number(e.target.value) })} /></td>
                    </>
                  )}
                  {type === 'RET' && (
                    <td>{l.assetId ? (
                      <select className="input" aria-label={t('sup.asset.condition')} value={l.condition ?? 'Good'} onChange={(e) => patch(l.key, { condition: e.target.value })}>
                        {CONDITIONS.map((c) => <option key={c} value={c}>{t(`sup.condition.${c}`)}</option>)}
                      </select>) : '—'}</td>
                  )}
                  <td className="row-actions">
                    {type === 'RCV' && asset && (
                      <button type="button" className="btn btn-ghost btn-sm" title={t('sup.doc.duplicatePiece')} aria-label={t('sup.doc.duplicatePiece')}
                        onClick={() => { setLines((ls) => [...ls, { ...l, key: seq, serialNumber: null }]); setSeq((s) => s + 1) }}><Plus size={14} aria-hidden /></button>
                    )}
                    <button type="button" className="btn btn-ghost btn-sm" title={t('sup.common.remove')} aria-label={t('sup.common.remove')}
                      onClick={() => setLines((ls) => ls.filter((x) => x.key !== l.key))}><Trash2 size={14} aria-hidden /></button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {type !== 'TRF' && (
        <button type="button" className="btn btn-ghost" style={{ marginTop: '.5rem' }} onClick={() => { setLines((ls) => [...ls, blankLine(seq)]); setSeq((s) => s + 1) }}>
          <Plus size={14} aria-hidden /> {t('sup.doc.addItem')}
        </button>
      )}
    </form>
  )
}
