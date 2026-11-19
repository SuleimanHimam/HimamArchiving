import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Archive, ArrowRight, History, Printer, Trash2, Wrench } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import { auth } from '../../lib/auth'
import { day } from '../../lib/hrHooks'
import { supply, money, ASSET_STATUSES } from '../../lib/supply'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { PageHead } from '../hr/hrShared'
import { AssetStatusPill, KindPill, Pager, WorkOrderStatusPill } from './supplyShared'

/** سجل الأصول (screen 13): every piece by its barcode — where it is, with whom, last counted. */
export default function AssetsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(25)
  const { data, isLoading, isError } = useQuery({
    queryKey: ['supply', 'assets', search, status, page, size],
    queryFn: () => supply.assets({ search: search || undefined, status: status || undefined, page, pageSize: size }),
    placeholderData: keepPreviousData,
  })

  return (
    <div>
      <PageHead title={t('sup.asset.title')} />
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.asset.subtitle')}</p>
      <section className="doc-card">
        <div className="sup-toolbar">
          <LabeledInput label={t('sup.asset.searchHint')} dir="auto" value={search} containerClassName="grow"
            onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
          <LabeledSelect label={t('hr.common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            <option value="">{t('hr.common.all')}</option>
            {ASSET_STATUSES.map((s) => <option key={s} value={s}>{t(`sup.assetStatus.${s}`)}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('sup.common.rows')} value={size} onChange={(e) => { setSize(Number(e.target.value)); setPage(1) }}>
            {[25, 50, 100].map((n) => <option key={n} value={n}>{n}</option>)}
          </LabeledSelect>
          <span className="muted">{data ? `${data.items.length} / ${data.totalCount}` : ''}</span>
        </div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <th>{t('sup.asset.number')}</th><th>{t('sup.common.barcode')}</th><th>{t('sup.item.item')}</th><th>{t('sup.asset.serial')}</th>
                <th>{t('hr.common.status')}</th><th>{t('sup.asset.holderOrPlace')}</th><th>{t('sup.asset.lastCounted')}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data?.items.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((a) => (
                <tr key={a.id} className="reg-row" onClick={() => navigate(`/app/supply-assets/${a.id}`)}>
                  <td dir="ltr" className="mono">{a.assetNumber}</td>
                  <td dir="ltr" className="mono muted">{a.barcode}</td>
                  <td>{a.itemName}</td>
                  <td dir="ltr" className="mono">{a.serialNumber ?? '—'}</td>
                  <td><AssetStatusPill status={a.status} /></td>
                  <td>{a.holderName ?? a.warehouseName ?? '—'}</td>
                  <td dir="ltr">{day(a.lastCountedDate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {data && <Pager page={page} totalPages={data.totalPages} onPage={setPage} />}
      </section>
    </div>
  )
}

/** بطاقة أصل (screen 14): facts, movement history, maintenance record, archived documents. */
export function AssetCardPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const id = Number(useParams().id)
  const { data, isLoading, isError } = useQuery({ queryKey: ['supply', 'assetCard', id], queryFn: () => supply.assetCard(id), enabled: id > 0 })
  if (isLoading) return <p className="muted">{t('hr.common.loading')}</p>
  if (isError || !data) return <p className="muted">{t('hr.common.loadFailed')}</p>
  const a = data.asset
  const canDispose = auth.hasPermission('SupplyDocuments.Create') && a.status !== 'Disposed'

  return (
    <div>
      <PageHead title={a.itemName}>
        {canDispose && (
          <button className="btn btn-seal" onClick={() => navigate(`/app/supply-documents?new=DSP&asset=${a.id}`)}><Trash2 size={15} aria-hidden /> {t('sup.asset.dispose')}</button>
        )}
        <button className="btn btn-ghost" onClick={() => window.print()}><Printer size={15} aria-hidden /> {t('sup.asset.printLabel')}</button>
        <button className="btn btn-ghost" onClick={() => navigate('/app/supply-assets')}><ArrowRight size={15} aria-hidden /> {t('sup.common.back')}</button>
      </PageHead>
      <p className="muted mono" dir="ltr" style={{ marginTop: '-.5rem', textAlign: 'end' }}>{a.assetNumber} — {a.barcode}</p>

      <div className="sup-grid2">
        <section className="sup-card">
          <div style={{ display: 'flex', gap: '.4rem', marginBottom: '.8rem' }}><AssetStatusPill status={a.status} /><KindPill kind="FixedAsset" /></div>
          <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-start', flexWrap: 'wrap' }}>
            <dl className="sup-facts" style={{ flex: 1 }}>
              <dt>{t('sup.asset.holder')}</dt><dd>{a.holderName ?? '—'}</dd>
              <dt>{t('sup.asset.place')}</dt><dd>{a.warehouseName ?? '—'}</dd>
              <dt>{t('sup.asset.serial')}</dt><dd dir="ltr">{a.serialNumber ?? '—'}</dd>
              <dt>{t('sup.asset.condition')}</dt><dd>{t(`sup.condition.${a.condition}`)}</dd>
              <dt>{t('sup.asset.purchaseDate')}</dt><dd dir="ltr">{day(a.purchaseDate)}</dd>
              <dt>{t('sup.asset.value')}</dt><dd>{money(a.value)}</dd>
              <dt>{t('sup.asset.warrantyEnd')}</dt><dd dir="ltr">{day(a.warrantyEndDate)}</dd>
              <dt>{t('sup.asset.lastCounted')}</dt><dd dir="ltr">{day(a.lastCountedDate)}</dd>
            </dl>
            {/* Label: QR of the barcode value (the approved library); a linear barcode font/library awaits approval. */}
            <figure style={{ margin: 0, textAlign: 'center' }}>
              <QRCodeSVG value={a.barcode} size={96} />
              <figcaption className="mono" dir="ltr" style={{ fontSize: '.8rem' }}>{a.barcode}</figcaption>
            </figure>
          </div>
        </section>
        <section className="sup-card">
          <h3><History size={16} aria-hidden /> {t('sup.asset.history')}</h3>
          {data.history.length === 0 ? <p className="muted">{t('hr.common.empty')}</p> : (
            <ul className="sup-timeline">
              {data.history.map((h, i) => (
                <li key={i}>
                  <strong>{t(`sup.event.${h.kind}`, { defaultValue: h.kind })}</strong>{h.documentNumber && <span className="mono" dir="ltr"> — {h.documentNumber}</span>}
                  <div>{h.description}</div>
                  <small dir="ltr">{day(h.at)}</small>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="sup-grid2">
        <section className="sup-card">
          <h3><Wrench size={16} aria-hidden /> {t('sup.asset.maintenance')}</h3>
          {data.suggestReplacement && <p className="status-pill s-overdue" style={{ display: 'inline-block' }}>{t('sup.asset.suggestReplacement')}</p>}
          <p className="muted">{t('sup.asset.faults', { count: data.faultCount })} · {t('sup.asset.cost')}: {money(data.maintenanceCost)}</p>
          {data.maintenance.length === 0 ? <p className="muted">{t('sup.asset.noMaintenance')}</p> : (
            <ul className="sup-list">
              {data.maintenance.map((w) => (
                <li key={w.id}><span><span className="mono" dir="ltr">{w.number}</span> · {w.description}</span><span><WorkOrderStatusPill status={w.status} /> {money(w.cost)}</span></li>
              ))}
            </ul>
          )}
        </section>
        <section className="sup-card">
          <h3><Archive size={16} aria-hidden /> {t('sup.asset.documents')}</h3>
          {data.history.filter((h) => h.documentId).length === 0 ? <p className="muted">{t('hr.common.empty')}</p> : (
            <ul className="sup-list">
              {data.history.filter((h) => h.documentId).map((h) => (
                <li key={h.documentId} className="reg-row" onClick={() => navigate(`/app/supply-documents?open=${h.documentId}`)}>
                  <span>{t(`sup.docType.${h.kind}`, { defaultValue: h.kind })}</span><span className="mono" dir="ltr">{h.documentNumber}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
