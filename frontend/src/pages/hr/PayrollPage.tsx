import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError, type EmployeeCategory } from '../../lib/hr'
import {
  hrTime, money, PAYROLL_STAGES, type DisbursementRule, type DisbursementSimulation, type PayrollPeriodDto, type PayslipDto,
} from '../../lib/hrTime'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { EmployeeSelect, MonthPicker } from './hrtShared'
import { thisMonth } from '../../lib/hrtUtils'
import { day } from '../../lib/hrHooks'
import './hrTime.css'

const CATEGORIES: EmployeeCategory[] = ['Military', 'Civil', 'Contract']
const RULE_LABEL = { percentOfSalary: 'hrt.pay.pctSalary', minimumAmount: 'hrt.pay.minimum', percentOfAccumulated: 'hrt.pay.pctAccumulated' } as const

/** شهر الرواتب (slides 16–18, image 29): eight stages, totals, differences, inputs, disbursement decision, payslips. */
export default function PayrollPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('HrPayroll.Edit')
  const periods = useQuery({ queryKey: ['hr', 'payroll'], queryFn: hrTime.periods })
  const [selected, setSelected] = useState<number | null>(null)
  const [open, setOpen] = useState(thisMonth())
  const current = periods.data?.find((p) => p.id === selected) ?? null

  async function openMonth(e: FormEvent) {
    e.preventDefault()
    try {
      const p = await hrTime.openPeriod(open.year, open.month)
      await qc.invalidateQueries({ queryKey: ['hr', 'payroll'] })
      setSelected(p.id)
      toast.success(t('hrt.pay.opened'))
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div>
      <PageHead title={t('hrt.pay.title')} />
      <section className="doc-card">
        <p className="muted">{t('hrt.pay.illustrative')}</p>
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('hrt.common.month')}</th><th>{t('hrt.pay.stage')}</th><th>{t('hrt.pay.employees')}</th><th>{t('hrt.pay.net')}</th><th>{t('hrt.pay.paid')}</th></tr></thead>
            <tbody>
              {periods.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {periods.data?.map((p) => (
                <tr key={p.id} className={p.id === selected ? 'reg-row is-editing' : 'reg-row'} style={{ cursor: 'pointer' }}
                  onClick={() => setSelected(p.id)} onKeyDown={(e) => { if (e.key === 'Enter') setSelected(p.id) }} tabIndex={0}>
                  <td dir="ltr">{p.year}-{String(p.month).padStart(2, '0')}</td><td>{t(`hrt.stage.${p.stage}`)}</td>
                  <td>{p.employees}</td><td dir="ltr">{money(p.net)}</td><td dir="ltr">{money(p.paid)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {canEdit && (
          <form className="form-grid" onSubmit={openMonth} style={{ marginTop: '1rem' }}>
            <MonthPicker year={open.year} month={open.month} onChange={(y, m) => setOpen({ year: y, month: m })} />
            <div className="form-actions"><button className="btn btn-primary">{t('hrt.pay.open')}</button></div>
          </form>
        )}
      </section>
      {current && <PeriodDetail key={current.id} period={current} />}
    </div>
  )
}

function PeriodDetail({ period: p }: { period: PayrollPeriodDto }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { prompt } = useConfirmDialog()
  const canEdit = auth.hasPermission('HrPayroll.Edit')
  const canApprove = auth.hasPermission('HrPayroll.Approve')
  const stageIndex = PAYROLL_STAGES.indexOf(p.stage)
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'payroll'] })
  const [warnings, setWarnings] = useState<string[]>([])

  async function run(fn: () => Promise<unknown>, done: string) {
    try { await fn(); toast.success(t(done)); await refresh() }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  const advance = () => run(() => hrTime.advance(p.id, null), 'hrt.pay.advanced')
  const signOff = () => run(() => hrTime.signOff(p.id, null), 'hrt.pay.advanced')
  const recalc = () => run(async () => setWarnings((await hrTime.recalculate(p.id)).warnings), 'hrt.pay.recalculated')
  async function sendBack() {
    const reason = await prompt({ title: t('hrt.pay.return'), label: t('hrt.pay.returnReason') })
    if (reason) await run(() => hrTime.returnPeriod(p.id, reason), 'hrt.pay.returned')
  }

  const prepNext = p.stage === 'Open' || p.stage === 'Inputs' || p.stage === 'Calculated'
  const signNext = p.stage === 'Reviewed' || p.stage === 'Audited' || p.stage === 'Disbursed'

  return (
    <section className="doc-card">
      <h3 className="detail-h3">{t('hrt.pay.month', { month: `${p.year}-${String(p.month).padStart(2, '0')}` })}</h3>
      <ol className="stage-bar" aria-label={t('hrt.pay.stage')}>
        {PAYROLL_STAGES.map((s, i) => {
          const log = [...p.log].reverse().find((l) => l.stage === s)
          return (
            <li key={s} className={i < stageIndex ? 'is-done' : i === stageIndex ? 'is-current' : undefined}
              title={log ? `${log.userName ?? ''} ${day(log.at)}${log.note ? ` — ${log.note}` : ''}` : undefined}>
              {i + 1}. {t(`hrt.stage.${s}`)}{log?.userName ? ` · ${log.userName}` : ''}
            </li>
          )
        })}
      </ol>
      <div className="kpi-row">
        <div>{t('hrt.pay.employees')}<strong>{p.employees}</strong></div>
        <div>{t('hrt.pay.gross')}<strong dir="ltr">{money(p.gross)}</strong></div>
        <div>{t('hrt.pay.deductions')}<strong dir="ltr">{money(p.deductions)}</strong></div>
        <div>{t('hrt.pay.net')}<strong dir="ltr">{money(p.net)}</strong></div>
      </div>
      <div className="row-actions" style={{ marginBottom: '1rem' }}>
        {prepNext && canEdit && <button className="btn btn-primary" onClick={advance}>{t(`hrt.pay.next.${p.stage}`)}</button>}
        {p.stage === 'Calculated' && canEdit && <button className="btn btn-ghost" onClick={recalc}>{t('hrt.pay.recalculate')}</button>}
        {signNext && canApprove && <button className="btn btn-primary" onClick={signOff}>{t(`hrt.pay.next.${p.stage}`)}</button>}
        {(p.stage === 'Calculated' || p.stage === 'Reviewed' || p.stage === 'Audited') && canApprove &&
          <button className="btn btn-seal" onClick={sendBack}>{t('hrt.pay.return')}</button>}
      </div>
      {warnings.length > 0 && <ul>{warnings.map((w) => <li key={w} style={{ color: 'var(--seal)' }}>{w}</li>)}</ul>}
      {p.log.some((l) => l.isReturn) && (
        <p className="muted">{t('hrt.pay.lastReturn')}: {[...p.log].reverse().find((l) => l.isReturn)?.note}</p>
      )}

      <Tabs defaultValue="results" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="results">{t('hrt.pay.results')}</TabsTrigger>
          <TabsTrigger value="inputs">{t('hrt.pay.inputs')}</TabsTrigger>
          <TabsTrigger value="disbursement">{t('hrt.pay.disbursement')}</TabsTrigger>
        </TabsList>
        <TabsContent value="results"><Results period={p} /></TabsContent>
        <TabsContent value="inputs"><Inputs period={p} /></TabsContent>
        <TabsContent value="disbursement"><Disbursement period={p} /></TabsContent>
      </Tabs>
    </section>
  )
}

function Results({ period: p }: { period: PayrollPeriodDto }) {
  const { t } = useTranslation()
  const rows = useQuery({ queryKey: ['hr', 'payroll', p.id, 'results', p.stage], queryFn: () => hrTime.results(p.id) })
  const [slipFor, setSlipFor] = useState<number | null>(null)
  const slip = useQuery({ queryKey: ['hr', 'payslip', p.id, slipFor], queryFn: () => hrTime.payslip(p.id, slipFor!), enabled: !!slipFor })
  return (
    <>
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hrt.common.employee')}</th><th>{t('hrt.common.category')}</th><th>{t('hrt.pay.gross')}</th><th>{t('hrt.pay.deductions')}</th>
            <th>{t('hrt.pay.net')}</th><th>{t('hrt.pay.difference')}</th><th>{t('hrt.pay.paid')}</th><th aria-label={t('hr.common.actions')} /></tr></thead>
          <tbody>
            {rows.data?.length === 0 && <tr><td colSpan={8} className="reg-empty">{t('hrt.pay.noResults')}</td></tr>}
            {rows.data?.map((r) => (
              <tr key={r.employeeId} className="reg-row">
                <td>{r.employeeNumber} · {r.employeeName}</td><td>{t(`hr.category.${r.category}`)}</td>
                <td dir="ltr">{money(r.gross)}</td><td dir="ltr">{money(r.deductions)}</td><td dir="ltr"><strong>{money(r.net)}</strong></td>
                <td dir="ltr" className={r.difference && r.difference < 0 ? 'num-neg' : undefined}>{r.difference == null ? t('hrt.pay.firstMonth') : money(r.difference)}</td>
                <td dir="ltr">{money(r.paid)}</td>
                <td><button className="btn btn-ghost btn-sm" onClick={() => setSlipFor(slipFor === r.employeeId ? null : r.employeeId)}>{t('hrt.pay.payslip')}</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {slip.data && <PayslipView slip={slip.data} />}
    </>
  )
}

export function PayslipView({ slip }: { slip: PayslipDto }) {
  const { t } = useTranslation()
  return (
    <div className="doc-card" style={{ marginTop: '1rem' }} aria-label={t('hrt.pay.payslip')}>
      <h4 className="detail-h3">{t('hrt.pay.payslipOf', { name: slip.employeeName, month: `${slip.year}-${String(slip.month).padStart(2, '0')}` })}</h4>
      <table className="reg-table">
        <thead><tr><th>{t('hrt.pay.element')}</th><th>{t('hrt.pay.quantity')}</th><th>{t('hrt.pay.rate')}</th><th>{t('hrt.pay.earning')}</th><th>{t('hrt.pay.deduction')}</th></tr></thead>
        <tbody>
          {slip.lines.map((l) => (
            <tr key={l.code} className="reg-row">
              <td>{l.name}</td><td dir="ltr">{l.quantity}</td><td dir="ltr">{l.rate}</td>
              <td dir="ltr">{l.kind === 'Earning' ? money(l.amount) : ''}</td><td dir="ltr">{l.kind === 'Deduction' ? money(l.amount) : ''}</td>
            </tr>
          ))}
          <tr><th>{t('hrt.pay.totals')}</th><td /><td /><th dir="ltr">{money(slip.gross)}</th><th dir="ltr">{money(slip.deductions)}</th></tr>
        </tbody>
      </table>
      <div className="kpi-row" style={{ marginTop: '.75rem' }}>
        <div>{t('hrt.pay.net')}<strong dir="ltr">{money(slip.net)}</strong></div>
        <div>{t('hrt.pay.paid')}<strong dir="ltr">{money(slip.paid)}</strong></div>
        <div>{t('hrt.pay.carried')}<strong dir="ltr">{money(slip.carried)}</strong></div>
        <div>{t('hrt.pay.entitlementBalance')}<strong dir="ltr">{money(slip.entitlementBalance)}</strong></div>
      </div>
    </div>
  )
}

function Inputs({ period: p }: { period: PayrollPeriodDto }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const editable = auth.hasPermission('HrPayroll.Edit') && (p.stage === 'Open' || p.stage === 'Inputs')
  const rows = useQuery({ queryKey: ['hr', 'payroll', p.id, 'inputs'], queryFn: () => hrTime.inputs(p.id) })
  const elements = (useQuery({ queryKey: ['hr', 'payElements', 'active'], queryFn: () => hrTime.elements(true) }).data ?? [])
    .filter((e) => e.method === 'PerUnit' && e.source === 'Manual')
  const [f, setF] = useState({ employeeId: null as number | null, payElementId: 0, quantity: '', note: '' })
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'payroll'] })

  async function add(e: FormEvent) {
    e.preventDefault()
    try {
      await hrTime.addInput(p.id, { employeeId: f.employeeId!, payElementId: f.payElementId, quantity: Number(f.quantity), note: f.note || null })
      setF((x) => ({ ...x, quantity: '', note: '' }))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  async function remove(id: number) {
    try { await hrTime.deleteInput(p.id, id); await refresh() }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <>
      <p className="muted">{t('hrt.pay.inputsHint')}</p>
      <table className="reg-table">
        <thead><tr><th>{t('hrt.common.employee')}</th><th>{t('hrt.pay.element')}</th><th>{t('hrt.pay.quantity')}</th><th>{t('hr.common.notes')}</th>{editable && <th aria-label={t('hr.common.actions')} />}</tr></thead>
        <tbody>
          {rows.data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {rows.data?.map((r) => (
            <tr key={r.id} className="reg-row">
              <td>{r.employeeName}</td><td>{r.payElementName}</td><td dir="ltr">{r.quantity}</td><td>{r.note ?? '—'}</td>
              {editable && <td><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.delete')} title={t('hr.common.delete')} onClick={() => remove(r.id)}><Trash2 size={14} aria-hidden /></button></td>}
            </tr>
          ))}
        </tbody>
      </table>
      {editable && (
        <form className="form-grid" onSubmit={add} style={{ marginTop: '1rem' }}>
          <EmployeeSelect label={t('hrt.common.employee')} required value={f.employeeId} onChange={(v) => setF((x) => ({ ...x, employeeId: v }))} />
          <LabeledSelect label={t('hrt.pay.element')} required value={f.payElementId || ''} onChange={(e) => setF((x) => ({ ...x, payElementId: Number(e.target.value) }))}>
            <option value="" />
            {elements.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </LabeledSelect>
          <LabeledInput label={t('hrt.pay.quantity')} type="number" step="0.01" dir="ltr" required value={f.quantity} onChange={(e) => setF((x) => ({ ...x, quantity: e.target.value }))} />
          <LabeledInput label={t('hr.common.notes')} value={f.note} onChange={(e) => setF((x) => ({ ...x, note: e.target.value }))} />
          <div className="form-actions"><button className="btn btn-primary">{t('hr.common.add')}</button></div>
        </form>
      )}
    </>
  )
}

function Disbursement({ period: p }: { period: PayrollPeriodDto }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const saved = useQuery({ queryKey: ['hr', 'payroll', p.id, 'rules'], queryFn: () => hrTime.disbursementRules(p.id) }).data
  const [rules, setRules] = useState<DisbursementRule[] | null>(null)
  const [liquidity, setLiquidity] = useState('')
  const [sim, setSim] = useState<DisbursementSimulation | null>(null)
  const current: DisbursementRule[] = rules ?? CATEGORIES.map((c) => saved?.find((r) => r.category === c)
    ?? { category: c, percentOfSalary: 0, minimumAmount: 0, percentOfAccumulated: 0 })
  const set = (c: EmployeeCategory, patch: Partial<DisbursementRule>) =>
    setRules(current.map((r) => (r.category === c ? { ...r, ...patch } : r)))
  const canApply = auth.hasPermission('HrPayroll.Approve') && p.stage === 'Approved'
  const liq = liquidity ? Number(liquidity) : null

  async function simulate() {
    try { setSim(await hrTime.simulate(p.id, current, liq)) }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  async function apply() {
    if (!await confirm({ message: t('hrt.pay.confirmDisburse'), destructive: true })) return
    try {
      await hrTime.disburse(p.id, current, liq)
      toast.success(t('hrt.pay.disbursed'))
      await qc.invalidateQueries({ queryKey: ['hr', 'payroll'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  if (PAYROLL_STAGES.indexOf(p.stage) < PAYROLL_STAGES.indexOf('Calculated'))
    return <p className="muted">{t('hrt.pay.calculateFirst')}</p>

  return (
    <>
      <p className="muted">{t('hrt.pay.disbursementHint')}</p>
      <table className="reg-table">
        <thead><tr><th>{t('hrt.common.category')}</th><th>{t('hrt.pay.pctSalary')}</th><th>{t('hrt.pay.minimum')}</th><th>{t('hrt.pay.pctAccumulated')}</th></tr></thead>
        <tbody>
          {current.map((r) => (
            <tr key={r.category}>
              <td>{t(`hr.category.${r.category}`)}</td>
              {(['percentOfSalary', 'minimumAmount', 'percentOfAccumulated'] as const).map((k) => (
                <td key={k}><input aria-label={`${t(`hr.category.${r.category}`)} — ${t(RULE_LABEL[k])}`} type="number" min={0} step="0.01" dir="ltr" style={{ width: '7rem' }}
                  disabled={p.stage !== 'Approved' && p.stage !== 'Calculated' && p.stage !== 'Reviewed' && p.stage !== 'Audited'}
                  value={r[k]} onChange={(e) => set(r.category, { [k]: Number(e.target.value) })} /></td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <div className="form-grid" style={{ marginTop: '.75rem' }}>
        <LabeledInput label={t('hrt.pay.liquidity')} type="number" min={0} dir="ltr" value={liquidity} onChange={(e) => setLiquidity(e.target.value)} />
        <div className="form-actions">
          <button className="btn btn-ghost" onClick={simulate}>{t('hrt.pay.simulate')}</button>
          {canApply && <button className="btn btn-primary" onClick={apply}>{t('hrt.pay.applyDecision')}</button>}
        </div>
      </div>
      {sim && (
        <>
          <div className="kpi-row">
            <div>{t('hrt.pay.totalPaid')}<strong dir="ltr" className={sim.withinLiquidity ? undefined : 'num-neg'}>{money(sim.totalPaid)}</strong></div>
            <div>{t('hrt.pay.totalCarried')}<strong dir="ltr">{money(sim.totalCarried)}</strong></div>
            {sim.availableLiquidity != null && <div>{t('hrt.pay.liquidity')}<strong dir="ltr">{money(sim.availableLiquidity)}</strong></div>}
          </div>
          {!sim.withinLiquidity && <p style={{ color: 'var(--seal)' }}>{t('hrt.pay.overLiquidity')}</p>}
          <table className="reg-table">
            <thead><tr><th>{t('hrt.common.category')}</th><th>{t('hrt.pay.employees')}</th><th>{t('hrt.pay.net')}</th><th>{t('hrt.pay.paid')}</th><th>{t('hrt.pay.carried')}</th></tr></thead>
            <tbody>
              {sim.categories.map((c) => (
                <tr key={c.category} className="reg-row"><td>{t(`hr.category.${c.category}`)}</td><td>{c.employees}</td>
                  <td dir="ltr">{money(c.net)}</td><td dir="ltr">{money(c.paid)}</td><td dir="ltr">{money(c.carried)}</td></tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  )
}
