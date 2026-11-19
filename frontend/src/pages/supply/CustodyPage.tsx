import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Printer } from 'lucide-react'
import { day, useOrgUnits } from '../../lib/hrHooks'
import { supply, money, num } from '../../lib/supply'
import { LabeledSelect } from '../../components/ui/labeled-input'
import { PageHead } from '../hr/hrShared'
import { EmployeeSelect } from '../hr/hrtShared'
import { AssetStatusPill, Folders } from './supplyShared'

type Mode = 'employee' | 'unit'

/** العهدة (screen 17): an employee's custody, or a department's — its assets, its staff's, and this month's consumption. */
export default function CustodyPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const units = useOrgUnits()
  const preset = Number(params.get('employee')) || null
  const [mode, setMode] = useState<Mode>(preset ? 'employee' : 'unit')
  const [employeeId, setEmployeeId] = useState<number | null>(preset)
  const [unitId, setUnitId] = useState<number | null>(null)
  const query = mode === 'employee' ? { employeeId: employeeId ?? undefined } : { orgUnitId: unitId ?? undefined, includeStaff: true }
  const enabled = mode === 'employee' ? !!employeeId : !!unitId
  const { data, isLoading, isError } = useQuery({ queryKey: ['supply', 'custody', mode, employeeId, unitId], queryFn: () => supply.custody(query), enabled })

  return (
    <div>
      <PageHead title={t('sup.custody.title')}>
        <button className="btn btn-ghost" disabled={!data} onClick={() => window.print()}><Printer size={15} aria-hidden /> {t('sup.custody.print')}</button>
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.custody.subtitle')}</p>
      <section className="doc-card" style={{ marginBottom: '1rem' }}>
        <Folders<Mode> value={mode} onChange={setMode} items={[{ key: 'employee', label: t('sup.custody.employee') }, { key: 'unit', label: t('sup.custody.unit') }]} />
        <div className="sup-toolbar">
          {mode === 'employee'
            ? <EmployeeSelect label={t('hrt.common.employee')} value={employeeId} onChange={setEmployeeId} />
            : (
              <LabeledSelect label={t('sup.req.unit')} value={unitId ?? ''} onChange={(e) => setUnitId(e.target.value ? Number(e.target.value) : null)}>
                <option value="" />
                {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
              </LabeledSelect>
            )}
          {data && (
            <span className="muted grow" style={{ textAlign: 'end' }}>
              {t('sup.custody.summary', { count: data.assetCount, value: money(data.totalValue) })}
              {mode === 'unit' && ` — ${t('sup.custody.consumption', { qty: num(data.consumptionThisMonth) })}`}
            </span>
          )}
        </div>
      </section>
      {enabled && (
        <section className="doc-card">
          <div className="table-scroll">
            <table className="reg-table">
              <thead>
                <tr><th>{t('sup.asset.number')}</th><th>{t('sup.item.item')}</th><th>{t('sup.asset.serial')}</th><th>{t('sup.asset.holder')}</th>
                  <th>{t('hr.common.status')}</th><th className="num">{t('sup.asset.value')}</th><th>{t('sup.asset.lastCounted')}</th></tr>
              </thead>
              <tbody>
                {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
                {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
                {data?.assets.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('sup.custody.none')}</td></tr>}
                {data?.assets.map((a) => (
                  <tr key={a.id} className="reg-row" onClick={() => navigate(`/app/supply-assets/${a.id}`)}>
                    <td dir="ltr" className="mono">{a.assetNumber}</td><td>{a.itemName}</td><td dir="ltr">{a.serialNumber ?? '—'}</td>
                    <td>{a.holderName}</td><td><AssetStatusPill status={a.status} /></td><td className="num">{money(a.value)}</td><td dir="ltr">{day(a.lastCountedDate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}
