import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ScanLine } from 'lucide-react'
import { supply, type AssetDto, type AssetStatus, type CountStatus, type DocStatus, type ItemKind, type RequestStatus, type WorkOrderStatus } from '../../lib/supply'
import { apiError } from '../../lib/hr'
import { useToast } from '../../components/toast'
import { LabeledInput } from '../../components/ui/labeled-input'
import '../documents/documents.css'
import './supply.css'

/* Pieces shared by the supply screens. */

const DOC_PILL: Record<DocStatus, string> = {
  Draft: 's-new', Submitted: 's-inprogress', AwaitingReceipt: 's-onhold', Executed: 's-closed', Rejected: 's-overdue', Returned: 's-onhold', Cancelled: 's-archived',
}
const REQ_PILL: Record<RequestStatus, string> = {
  Draft: 's-new', Submitted: 's-inprogress', Approved: 's-assigned', PartiallyIssued: 's-onhold', Issued: 's-closed', Suspended: 's-overdue',
  Rejected: 's-overdue', Returned: 's-onhold', Cancelled: 's-archived',
}
const ASSET_PILL: Record<AssetStatus, string> = {
  InStock: 's-closed', InCustody: 's-new', Maintenance: 's-onhold', PendingDisposal: 's-overdue', Disposed: 's-archived', Missing: 's-overdue',
}
const COUNT_PILL: Record<CountStatus, string> = {
  Planning: 's-new', Counting: 's-inprogress', Reconciled: 's-onhold', Settling: 's-assigned', Closed: 's-closed', Cancelled: 's-archived',
}
const WO_PILL: Record<WorkOrderStatus, string> = {
  New: 's-new', InProgress: 's-inprogress', AwaitingParts: 's-onhold', Closed: 's-closed', RecommendedDisposal: 's-overdue', Cancelled: 's-archived',
}

export const DocStatusPill = ({ status }: { status: DocStatus }) => <Pill cls={DOC_PILL[status]} k={`sup.docStatus.${status}`} />
export const RequestStatusPill = ({ status }: { status: RequestStatus }) => <Pill cls={REQ_PILL[status]} k={`sup.reqStatus.${status}`} />
export const AssetStatusPill = ({ status }: { status: AssetStatus }) => <Pill cls={ASSET_PILL[status]} k={`sup.assetStatus.${status}`} />
export const CountStatusPill = ({ status }: { status: CountStatus }) => <Pill cls={COUNT_PILL[status]} k={`sup.countStatus.${status}`} />
export const WorkOrderStatusPill = ({ status }: { status: WorkOrderStatus }) => <Pill cls={WO_PILL[status]} k={`sup.woStatus.${status}`} />

function Pill({ cls, k }: { cls: string; k: string }) {
  const { t } = useTranslation()
  return <span className={`status-pill ${cls}`}>{t(k)}</span>
}

export function KindPill({ kind }: { kind: ItemKind | string }) {
  const { t } = useTranslation()
  return <span className={`sup-kind sup-kind--${kind}`}>{t(`sup.kind.${kind}`)}</span>
}

/** Folder chips with counters (list-page pattern). */
export function Folders<K extends string>({ items, value, onChange }: {
  items: { key: K; label: string; count?: number }[]; value: K; onChange: (k: K) => void
}) {
  return (
    <div className="sup-folders" role="tablist">
      {items.map((f) => (
        <button key={f.key} type="button" role="tab" aria-selected={value === f.key} className={value === f.key ? 'is-active' : ''} onClick={() => onChange(f.key)}>
          {f.label}{f.count != null && <span className="sup-count">{f.count}</span>}
        </button>
      ))}
    </div>
  )
}

export function Pager({ page, totalPages, onPage }: { page: number; totalPages: number; onPage: (p: number) => void }) {
  const { t } = useTranslation()
  if (totalPages <= 1) return null
  return (
    <div className="row-actions" style={{ justifyContent: 'flex-end', marginTop: '.75rem' }}>
      <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t('hr.common.prev')}</button>
      <span className="muted">{page} / {totalPages}</span>
      <button className="btn btn-ghost btn-sm" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>{t('hr.common.next')}</button>
    </div>
  )
}

export function Kpi({ icon, value, label, alert }: { icon: ReactNode; value: ReactNode; label: string; alert?: boolean }) {
  return (
    <div className={`sup-kpi${alert ? ' sup-kpi--alert' : ''}`}>
      <span className="sup-kpi__icon" aria-hidden>{icon}</span>
      <div><strong>{value}</strong><span>{label}</span></div>
    </div>
  )
}

/**
 * Barcode field: a scanner types the code and sends Enter. The piece is looked up on the server and handed
 * to the caller; nothing about expected quantities is ever shown here.
 */
export function BarcodeInput({ onAsset, placeholder, lookup = true, onCode }: {
  onAsset?: (a: AssetDto) => void; placeholder?: string; lookup?: boolean; onCode?: (code: string) => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const [code, setCode] = useState('')
  async function go() {
    const c = code.trim()
    if (!c) return
    if (!lookup) { onCode?.(c); setCode(''); return }
    try {
      onAsset?.(await supply.byBarcode(c))
      setCode('')
    } catch (err) { toast.error(apiError(err, t('sup.common.notFound'))) }
  }
  return (
    <div className="sup-scan">
      <LabeledInput label={t('sup.common.barcode')} data-scan dir="ltr" value={code} containerClassName="grow" title={placeholder ?? t('sup.common.scanHint')}
        onChange={(e) => setCode(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void go() } }} />
      <button type="button" className="btn btn-ghost" onClick={() => void go()}><ScanLine size={16} aria-hidden /> {t('sup.common.add')}</button>
    </div>
  )
}

