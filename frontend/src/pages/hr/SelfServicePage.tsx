import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { hrTime, money, type LeaveRequestDto } from '../../lib/hrTime'
import { PageHead } from './hrShared'
import { MonthPicker } from './hrtShared'
import { thisMonth } from '../../lib/hrtUtils'
import { BalanceTable, LeaveForm, LeaveTable } from './LeavePage'
import { PayslipView } from './PayrollPage'
import { day } from '../../lib/hrHooks'

/** الخدمة الذاتية (image 03): my balances, my requests, my attendance, my payslips and entitlement balance. */
export default function SelfServicePage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const year = new Date().getFullYear()
  const [form, setForm] = useState<LeaveRequestDto | 'new' | null>(null)
  const [month, setMonth] = useState(thisMonth())
  const [openSlip, setOpenSlip] = useState<number | null>(null)

  const balances = useQuery({ queryKey: ['hr', 'me', 'balances', year], queryFn: () => hrTime.myBalances(year), retry: false })
  // The balances call answers 400 when the login has no employee record; everything else waits for it.
  const linked = balances.isSuccess
  const leaves = useQuery({ queryKey: ['hr', 'me', 'leaves'], queryFn: hrTime.myLeaves, enabled: linked })
  const attendance = useQuery({ queryKey: ['hr', 'me', 'attendance', month], queryFn: () => hrTime.myAttendance(month.year, month.month), retry: false, enabled: linked })
  const slips = useQuery({ queryKey: ['hr', 'me', 'payslips'], queryFn: hrTime.myPayslips, enabled: linked })
  const ent = useQuery({ queryKey: ['hr', 'me', 'entitlements'], queryFn: hrTime.myEntitlements, retry: false, enabled: linked })
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'me'] })

  if (balances.isLoading) return <div><PageHead title={t('hrt.self.title')} /><p className="muted">{t('hr.common.loading')}</p></div>
  if (balances.isError)
    return <div><PageHead title={t('hrt.self.title')} /><section className="doc-card"><p className="muted">{t('hrt.self.notLinked')}</p></section></div>

  return (
    <div>
      <PageHead title={t('hrt.self.title')}>
        {!form && <button className="btn btn-primary" onClick={() => setForm('new')}><Plus size={15} aria-hidden /> {t('hrt.leave.new')}</button>}
      </PageHead>

      {form && (
        <LeaveForm key={form === 'new' ? 'new' : form.id} existing={form === 'new' ? null : form}
          onDone={async () => { setForm(null); await refresh() }} onCancel={() => setForm(null)} />
      )}

      <section className="doc-card">
        <h3 className="detail-h3">{t('hrt.self.balances', { year })}</h3>
        <BalanceTable rows={balances.data} loading={balances.isLoading} />
      </section>

      <section className="doc-card">
        <h3 className="detail-h3">{t('hrt.self.requests')}</h3>
        <LeaveTable rows={leaves.data} loading={leaves.isLoading} failed={leaves.isError} onEdit={(r) => setForm(r)} onChanged={refresh} />
      </section>

      <section className="doc-card">
        <h3 className="detail-h3">{t('hrt.self.attendance')}</h3>
        <div className="form-grid"><MonthPicker year={month.year} month={month.month} onChange={(y, m) => setMonth({ year: y, month: m })} /></div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('hrt.att.date')}</th><th>{t('hrt.att.shift')}</th><th>{t('hrt.att.in')}</th><th>{t('hrt.att.out')}</th><th>{t('hr.common.status')}</th></tr></thead>
            <tbody>
              {attendance.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {attendance.data?.map((d) => (
                <tr key={d.date} className={d.state === 'Exception' ? 'reg-row is-overdue' : 'reg-row'}>
                  <td dir="ltr">{day(d.date)}</td><td>{d.shiftName ?? '—'}</td><td dir="ltr">{d.firstIn ?? '—'}</td><td dir="ltr">{d.lastOut ?? '—'}</td>
                  <td>{t(`hrt.dayState.${d.state}`)}{d.exceptions.map((x) => ` · ${t(`hrt.exKind.${x.kind}`)}`).join('')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="doc-card">
        <h3 className="detail-h3">{t('hrt.self.payslips')}</h3>
        {ent.data && <p>{t('hrt.pay.entitlementBalance')}: <strong dir="ltr">{money(ent.data.balance)}</strong> ₪</p>}
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('hrt.common.month')}</th><th>{t('hrt.pay.net')}</th><th>{t('hrt.pay.paid')}</th><th>{t('hrt.pay.carried')}</th><th aria-label={t('hr.common.actions')} /></tr></thead>
            <tbody>
              {slips.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {slips.data?.map((s) => (
                <tr key={s.periodId} className="reg-row">
                  <td dir="ltr">{s.year}-{String(s.month).padStart(2, '0')}</td><td dir="ltr">{money(s.net)}</td>
                  <td dir="ltr">{money(s.paid)}</td><td dir="ltr">{money(s.carried)}</td>
                  <td><button className="btn btn-ghost btn-sm" onClick={() => setOpenSlip(openSlip === s.periodId ? null : s.periodId)}>{t('hrt.pay.payslip')}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {openSlip && slips.data?.find((s) => s.periodId === openSlip) && <PayslipView slip={slips.data.find((s) => s.periodId === openSlip)!} />}
      </section>
    </div>
  )
}
