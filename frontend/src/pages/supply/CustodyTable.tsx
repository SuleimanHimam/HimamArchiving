import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { day } from '../../lib/hrHooks'
import { supply, money } from '../../lib/supply'
import { AssetStatusPill } from './supplyShared'

/** Read-only custody list: the «العهدة» tab of the personnel file (§8.2) and «عهدتي» in self-service. */
export default function CustodyTable({ employeeId }: { employeeId: number | 'me' }) {
  const { t } = useTranslation()
  const { data, isLoading, isError } = useQuery({
    queryKey: ['supply', 'custodyOf', employeeId],
    queryFn: () => (employeeId === 'me' ? supply.myCustody() : supply.employeeCustody(employeeId)),
    retry: false,
  })
  return (
    <div className="table-scroll">
      <table className="reg-table">
        <thead><tr><th>{t('sup.asset.number')}</th><th>{t('sup.item.item')}</th><th>{t('sup.asset.serial')}</th><th>{t('hr.common.status')}</th>
          <th className="num">{t('sup.asset.value')}</th><th>{t('sup.asset.lastCounted')}</th></tr></thead>
        <tbody>
          {isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
          {isError && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
          {data?.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('sup.custody.none')}</td></tr>}
          {data?.map((a) => (
            <tr key={a.id}>
              <td dir="ltr" className="mono">{a.assetNumber}</td><td>{a.itemName}</td><td dir="ltr">{a.serialNumber ?? '—'}</td>
              <td><AssetStatusPill status={a.status} /></td><td className="num">{money(a.value)}</td><td dir="ltr">{day(a.lastCountedDate)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
