import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Barcode, BarChart3, BellRing, ClipboardCheck, Plus, Wrench } from 'lucide-react'
import { auth } from '../../lib/auth'
import { supply, num } from '../../lib/supply'
import { PageHead } from '../hr/hrShared'
import { Kpi, WorkOrderStatusPill } from './supplyShared'

// Series colours come from theme tokens; a unit keeps its colour across months.
const SERIES = ['var(--ink)', 'var(--ok)', 'var(--brass)', 'var(--accent)', 'var(--seal)', 'var(--warn)', 'var(--c-confidential)', 'var(--muted)']

/** لوحة اللوازم (screen 11): critical stock, waiting requests, open work orders, custody, consumption, alerts. */
export default function SupplyDashboardPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data, isLoading, isError } = useQuery({ queryKey: ['supply', 'dashboard'], queryFn: supply.dashboard })

  const chart = useMemo(() => {
    const points = data?.consumption ?? []
    const units = [...new Map(points.map((p) => [p.orgUnitId, p.orgUnitName])).entries()]
    const months = [...new Set(points.map((p) => p.year * 100 + p.month))].sort()
    const cols = months.map((ym) => {
      const parts = units.map(([id], i) => ({ color: SERIES[i % SERIES.length], q: points.find((p) => p.year * 100 + p.month === ym && p.orgUnitId === id)?.quantity ?? 0 }))
      return { ym, parts, total: parts.reduce((s, p) => s + p.q, 0) }
    })
    const max = Math.max(1, ...cols.map((c) => c.total))
    return { units, cols, max }
  }, [data])

  if (isLoading) return <p className="muted">{t('hr.common.loading')}</p>
  if (isError || !data) return <p className="muted">{t('hr.common.loadFailed')}</p>

  return (
    <div>
      <PageHead title={t('sup.dash.title')}>
        {auth.hasPermission('SupplyRequests.Create') && (
          <button className="btn btn-ghost" onClick={() => navigate('/app/supply-requests?new=1')}><ClipboardCheck size={15} aria-hidden /> {t('sup.req.new')}</button>
        )}
        {auth.hasPermission('SupplyDocuments.Create') && (
          <button className="btn btn-primary" onClick={() => navigate('/app/supply-documents?new=RCV')}><Plus size={15} aria-hidden /> {t('sup.doc.newRCV')}</button>
        )}
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.dash.subtitle')}</p>

      <div className="sup-kpis">
        <Kpi icon={<AlertTriangle size={18} />} value={data.criticalStock} label={t('sup.dash.critical')} alert={data.criticalStock > 0} />
        <Kpi icon={<ClipboardCheck size={18} />} value={data.pendingRequests} label={t('sup.dash.pending')} />
        <Kpi icon={<Wrench size={18} />} value={data.openWorkOrders} label={t('sup.dash.openOrders')} />
        <Kpi icon={<Barcode size={18} />} value={data.assetsInCustody} label={t('sup.dash.inCustody')} />
      </div>

      <div className="sup-grid2">
        <section className="sup-card">
          <h3><BarChart3 size={16} aria-hidden /> {t('sup.dash.consumption')}</h3>
          {chart.cols.length === 0 ? <p className="muted">{t('hr.common.empty')}</p> : (
            <>
              <div className="sup-bars" role="img" aria-label={t('sup.dash.consumption')}>
                {chart.cols.map((c) => (
                  <div key={c.ym} className="sup-bars__col">
                    <span className="sup-bars__total">{num(c.total)}</span>
                    <div className="sup-bars__stack" style={{ blockSize: `${(c.total / chart.max) * 100}%` }}>
                      {c.parts.filter((p) => p.q > 0).map((p, i) => (
                        <span key={i} style={{ background: p.color, flexGrow: p.q, minBlockSize: 2 }} title={num(p.q)} />
                      ))}
                    </div>
                    <span className="sup-bars__label">{t(`sup.months.${c.ym % 100}`)}</span>
                  </div>
                ))}
              </div>
              <div className="sup-legend">
                {chart.units.map(([id, name], i) => <span key={id}><i style={{ background: SERIES[i % SERIES.length] }} />{name}</span>)}
              </div>
            </>
          )}
        </section>

        <section className="sup-card">
          <h3 className="is-alert"><AlertTriangle size={16} aria-hidden /> {t('sup.dash.lowStock')}</h3>
          <div className="table-scroll">
            <table className="reg-table">
              <thead><tr><th>{t('sup.item.item')}</th><th className="num">{t('sup.item.balance')}</th><th className="num">{t('sup.item.reorder')}</th></tr></thead>
              <tbody>
                {data.lowStock.length === 0 && <tr><td colSpan={3} className="reg-empty">{t('sup.dash.noLow')}</td></tr>}
                {data.lowStock.map((l) => (
                  <tr key={l.itemId}>
                    <td>{l.itemName}</td>
                    <td className="num"><span className="is-low-num">{num(l.balance)} {l.unit}</span></td>
                    <td className="num">{num(l.reorderLevel)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <div className="sup-grid2">
        <section className="sup-card">
          <h3><ClipboardCheck size={16} aria-hidden /> {t('sup.dash.awaiting')}</h3>
          {data.awaitingFulfilment.length === 0 ? <p className="muted">{t('hr.common.empty')}</p> : (
            <ul className="sup-list">
              {data.awaitingFulfilment.map((r) => (
                <li key={r.id} className="reg-row" onClick={() => navigate(`/app/supply-requests?open=${r.id}`)}>
                  <span><span className="mono" dir="ltr">{r.number}</span> · {r.requesterName} — {r.orgUnitName}</span>
                  <span className="muted">{t('sup.dash.itemsCount', { count: r.lines.length })}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
        <section className="sup-card">
          <h3><Wrench size={16} aria-hidden /> {t('sup.dash.openOrders')}</h3>
          {data.workOrders.length === 0 ? <p className="muted">{t('hr.common.empty')}</p> : (
            <ul className="sup-list">
              {data.workOrders.map((w) => (
                <li key={w.id} className="reg-row" onClick={() => navigate('/app/supply-maintenance')}>
                  <span><span className="mono" dir="ltr">{w.number}</span> · {w.description}</span>
                  <WorkOrderStatusPill status={w.status} />
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {data.alerts.length > 0 && (
        <section className="sup-card">
          <h3><BellRing size={16} aria-hidden /> {t('sup.dash.alerts')}</h3>
          <ul className="sup-list">
            {data.alerts.map((a, i) => (
              <li key={i}><span><strong>{t(`sup.alert.${a.kind}`)}</strong> · {a.title}</span><span className="muted">{a.detail}</span></li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
