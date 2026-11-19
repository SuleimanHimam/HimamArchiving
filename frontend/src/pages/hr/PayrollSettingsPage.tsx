import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { hrTime, money, PAY_METHODS, PAY_SOURCES, type PayElementDto, type SavePayElement } from '../../lib/hrTime'
import { useLookups, day } from '../../lib/hrHooks'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { PageHead } from './hrShared'
import { CategoryChecks, Check, EmployeeSelect } from './hrtShared'
import { todayHebron } from '../../lib/hrtUtils'

/** عناصر الراتب وسلم الرواتب: elements, dated rates, the scale, tax tables and employee-specific amounts. All figures are data. */
export default function PayrollSettingsPage() {
  const { t, i18n } = useTranslation()
  const canEdit = auth.hasPermission('HrPayroll.Edit')
  return (
    <div>
      <PageHead title={t('hrt.paySet.title')} />
      <p className="muted">{t('hrt.pay.illustrative')}</p>
      <Tabs defaultValue="elements" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="elements">{t('hrt.paySet.elements')}</TabsTrigger>
          <TabsTrigger value="rates">{t('hrt.paySet.rates')}</TabsTrigger>
          <TabsTrigger value="scale">{t('hrt.paySet.scale')}</TabsTrigger>
          <TabsTrigger value="tax">{t('hrt.paySet.tax')}</TabsTrigger>
          <TabsTrigger value="employee">{t('hrt.paySet.employee')}</TabsTrigger>
        </TabsList>
        <TabsContent value="elements"><Elements canEdit={canEdit} /></TabsContent>
        <TabsContent value="rates"><Rates canEdit={canEdit} /></TabsContent>
        <TabsContent value="scale"><Scale canEdit={canEdit} /></TabsContent>
        <TabsContent value="tax"><Tax canEdit={canEdit} /></TabsContent>
        <TabsContent value="employee"><EmployeeAmounts canEdit={canEdit} /></TabsContent>
      </Tabs>
    </div>
  )
}

const useElements = () => useQuery({ queryKey: ['hr', 'payElements', 'all'], queryFn: () => hrTime.elements(false) }).data ?? []

function useSaver() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  return async (fn: () => Promise<unknown>) => {
    try { await fn(); toast.success(t('hr.common.saved')); await qc.invalidateQueries({ queryKey: ['hr'] }); return true }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))); return false }
  }
}

function Elements({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const save = useSaver()
  const elements = useElements()
  const empty: SavePayElement = {
    code: '', name: '', kind: 'Earning', method: 'Fixed', source: 'Manual', isTaxable: true, isPreTax: false,
    appliesToMilitary: true, appliesToCivil: true, appliesToContract: true, sortOrder: 0, isActive: true,
  }
  const [editing, setEditing] = useState<PayElementDto | null>(null)
  const [f, setF] = useState<SavePayElement>(empty)
  const reset = () => { setEditing(null); setF(empty) }
  const set = (p: Partial<SavePayElement>) => setF((x) => ({ ...x, ...p }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (await save(() => editing ? hrTime.updateElement(editing.id, { ...f, rowVersion: editing.rowVersion }) : hrTime.createElement(f))) reset()
  }

  return (
    <section className="doc-card">
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hr.settings.code')}</th><th>{t('hr.settings.name')}</th><th>{t('hrt.paySet.kind')}</th><th>{t('hrt.paySet.method')}</th>
            <th>{t('hrt.paySet.source')}</th><th>{t('hrt.paySet.taxable')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {elements.length === 0 && <tr><td colSpan={8} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {elements.map((x) => (
              <tr key={x.id} className="reg-row">
                <td dir="ltr">{x.code}</td><td>{x.name}</td><td>{t(`hrt.payKind.${x.kind}`)}</td><td>{t(`hrt.method.${x.method}`)}</td>
                <td>{x.method === 'PerUnit' ? t(`hrt.paySource.${x.source}`) : '—'}</td>
                <td>{x.isTaxable ? t('hr.common.yes') : x.isPreTax ? t('hrt.paySet.preTax') : t('hr.common.no')}</td>
                <td>{x.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && <td><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.edit')} title={t('hr.common.edit')}
                  onClick={() => { setEditing(x); setF({ ...x }) }}><Pencil size={14} aria-hidden /></button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editing ? t('hr.common.edit') : t('hrt.paySet.addElement')}</h4>
          <LabeledInput label={t('hr.settings.code')} required dir="ltr" value={f.code} onChange={(e) => set({ code: e.target.value })} />
          <LabeledInput label={t('hr.settings.name')} required value={f.name} onChange={(e) => set({ name: e.target.value })} />
          <LabeledSelect label={t('hrt.paySet.kind')} value={f.kind} onChange={(e) => set({ kind: e.target.value as SavePayElement['kind'] })}>
            <option value="Earning">{t('hrt.payKind.Earning')}</option><option value="Deduction">{t('hrt.payKind.Deduction')}</option>
          </LabeledSelect>
          <LabeledSelect label={t('hrt.paySet.method')} value={f.method} onChange={(e) => set({ method: e.target.value as SavePayElement['method'], source: 'Manual' })}>
            {PAY_METHODS.map((m) => <option key={m} value={m}>{t(`hrt.method.${m}`)}</option>)}
          </LabeledSelect>
          {f.method === 'PerUnit' && (
            <LabeledSelect label={t('hrt.paySet.source')} value={f.source} onChange={(e) => set({ source: e.target.value as SavePayElement['source'] })}>
              {PAY_SOURCES.map((s) => <option key={s} value={s}>{t(`hrt.paySource.${s}`)}</option>)}
            </LabeledSelect>
          )}
          <LabeledInput label={t('hrt.paySet.sortOrder')} type="number" dir="ltr" value={f.sortOrder} onChange={(e) => set({ sortOrder: Number(e.target.value) })} />
          {f.kind === 'Earning'
            ? <Check label={t('hrt.paySet.taxable')} checked={f.isTaxable} onChange={(v) => set({ isTaxable: v })} />
            : <Check label={t('hrt.paySet.preTax')} checked={f.isPreTax} onChange={(v) => set({ isPreTax: v })} />}
          <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => set({ isActive: v })} />
          <CategoryChecks value={f} onChange={set} />
          <div className="form-actions">
            <button className="btn btn-primary">{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={reset}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}

function CategorySelect({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const { t } = useTranslation()
  return (
    <LabeledSelect label={t('hrt.common.category')} value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">{t('hrt.paySet.allCategories')}</option>
      {(['Military', 'Civil', 'Contract'] as const).map((c) => <option key={c} value={c}>{t(`hr.category.${c}`)}</option>)}
    </LabeledSelect>
  )
}

function Rates({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const save = useSaver()
  const elements = useElements().filter((e) => e.method !== 'Basic' && e.method !== 'Tax')
  const rates = useQuery({ queryKey: ['hr', 'payRates'], queryFn: hrTime.rates }).data ?? []
  const [f, setF] = useState({ payElementId: 0, category: '', effectiveFrom: todayHebron(), value: '' })
  const name = (id: number) => elements.find((e) => e.id === id)?.name ?? id
  async function submit(e: FormEvent) {
    e.preventDefault()
    if (await save(() => hrTime.addRate({ payElementId: f.payElementId, category: f.category || null, effectiveFrom: f.effectiveFrom, value: Number(f.value) })))
      setF((x) => ({ ...x, value: '' }))
  }
  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.paySet.datedHint')}</p>
      <table className="reg-table">
        <thead><tr><th>{t('hrt.pay.element')}</th><th>{t('hrt.common.category')}</th><th>{t('hrt.paySet.from')}</th><th>{t('hrt.paySet.value')}</th></tr></thead>
        <tbody>
          {rates.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {rates.map((r) => (
            <tr key={r.id} className="reg-row"><td>{name(r.payElementId)}</td><td>{r.category ? t(`hr.category.${r.category}`) : t('hrt.paySet.allCategories')}</td>
              <td dir="ltr">{day(r.effectiveFrom)}</td><td dir="ltr">{r.value}</td></tr>
          ))}
        </tbody>
      </table>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <LabeledSelect label={t('hrt.pay.element')} required value={f.payElementId || ''} onChange={(e) => setF((x) => ({ ...x, payElementId: Number(e.target.value) }))}>
            <option value="" />{elements.map((x) => <option key={x.id} value={x.id}>{x.name} ({t(`hrt.method.${x.method}`)})</option>)}
          </LabeledSelect>
          <CategorySelect value={f.category} onChange={(v) => setF((x) => ({ ...x, category: v }))} />
          <LabeledInput label={t('hrt.paySet.from')} type="date" dir="ltr" required value={f.effectiveFrom} onChange={(e) => setF((x) => ({ ...x, effectiveFrom: e.target.value }))} />
          <LabeledInput label={t('hrt.paySet.value')} type="number" step="0.0001" min={0} dir="ltr" required value={f.value} onChange={(e) => setF((x) => ({ ...x, value: e.target.value }))} />
          <div className="form-actions"><button className="btn btn-primary">{t('hr.common.add')}</button></div>
        </form>
      )}
    </section>
  )
}

function Scale({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const save = useSaver()
  const grades = useLookups('grade')
  const scale = useQuery({ queryKey: ['hr', 'payScale'], queryFn: hrTime.scale }).data ?? []
  const [f, setF] = useState({ gradeId: 0, category: '', effectiveFrom: todayHebron(), basicAmount: '' })
  async function submit(e: FormEvent) {
    e.preventDefault()
    if (await save(() => hrTime.addScale({ gradeId: f.gradeId, category: f.category || null, effectiveFrom: f.effectiveFrom, basicAmount: Number(f.basicAmount) })))
      setF((x) => ({ ...x, basicAmount: '' }))
  }
  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.paySet.scaleHint')}</p>
      <table className="reg-table">
        <thead><tr><th>{t('hr.file.grade')}</th><th>{t('hrt.common.category')}</th><th>{t('hrt.paySet.from')}</th><th>{t('hrt.paySet.basic')}</th></tr></thead>
        <tbody>
          {scale.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {scale.map((s) => (
            <tr key={s.id} className="reg-row"><td>{s.gradeName}</td><td>{s.category ? t(`hr.category.${s.category}`) : t('hrt.paySet.allCategories')}</td>
              <td dir="ltr">{day(s.effectiveFrom)}</td><td dir="ltr">{money(s.basicAmount)}</td></tr>
          ))}
        </tbody>
      </table>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <LabeledSelect label={t('hr.file.grade')} required value={f.gradeId || ''} onChange={(e) => setF((x) => ({ ...x, gradeId: Number(e.target.value) }))}>
            <option value="" />{grades.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </LabeledSelect>
          <CategorySelect value={f.category} onChange={(v) => setF((x) => ({ ...x, category: v }))} />
          <LabeledInput label={t('hrt.paySet.from')} type="date" dir="ltr" required value={f.effectiveFrom} onChange={(e) => setF((x) => ({ ...x, effectiveFrom: e.target.value }))} />
          <LabeledInput label={t('hrt.paySet.basic')} type="number" step="0.01" min={0} dir="ltr" required value={f.basicAmount} onChange={(e) => setF((x) => ({ ...x, basicAmount: e.target.value }))} />
          <div className="form-actions"><button className="btn btn-primary">{t('hr.common.add')}</button></div>
        </form>
      )}
    </section>
  )
}

function Tax({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const save = useSaver()
  const tax = useQuery({ queryKey: ['hr', 'payTax'], queryFn: hrTime.tax }).data ?? []
  const [from, setFrom] = useState(todayHebron())
  const [lines, setLines] = useState([{ fromAmount: '0', toAmount: '', rate: '' }])
  async function submit(e: FormEvent) {
    e.preventDefault()
    if (await save(() => hrTime.saveTax({
      effectiveFrom: from,
      brackets: lines.map((l) => ({ fromAmount: Number(l.fromAmount), toAmount: l.toAmount === '' ? null : Number(l.toAmount), rate: Number(l.rate) })),
    }))) setLines([{ fromAmount: '0', toAmount: '', rate: '' }])
  }
  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.paySet.taxHint')}</p>
      <table className="reg-table">
        <thead><tr><th>{t('hrt.paySet.from')}</th><th>{t('hrt.paySet.bracketFrom')}</th><th>{t('hrt.paySet.bracketTo')}</th><th>{t('hrt.paySet.rate')}</th></tr></thead>
        <tbody>
          {tax.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {tax.map((b) => <tr key={b.id} className="reg-row"><td dir="ltr">{day(b.effectiveFrom)}</td><td dir="ltr">{money(b.fromAmount)}</td>
            <td dir="ltr">{b.toAmount == null ? '∞' : money(b.toAmount)}</td><td dir="ltr">{b.rate}%</td></tr>)}
        </tbody>
      </table>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <LabeledInput label={t('hrt.paySet.from')} type="date" dir="ltr" required value={from} onChange={(e) => setFrom(e.target.value)} />
          {lines.map((l, i) => (
            <div key={i} className="field--wide" style={{ display: 'flex', gap: '.5rem' }}>
              {(['fromAmount', 'toAmount', 'rate'] as const).map((k) => (
                <LabeledInput key={k} label={t(k === 'fromAmount' ? 'hrt.paySet.bracketFrom' : k === 'toAmount' ? 'hrt.paySet.bracketTo' : 'hrt.paySet.rate')}
                  type="number" step="0.01" min={0} dir="ltr" required={k !== 'toAmount'} value={l[k]}
                  onChange={(e) => setLines((ls) => ls.map((x, j) => (j === i ? { ...x, [k]: e.target.value } : x)))} />
              ))}
            </div>
          ))}
          <div className="form-actions">
            <button type="button" className="btn btn-ghost" onClick={() => setLines((ls) => [...ls, { fromAmount: ls[ls.length - 1]?.toAmount ?? '', toAmount: '', rate: '' }])}>{t('hrt.paySet.addBracket')}</button>
            <button className="btn btn-primary">{t('hr.common.save')}</button>
          </div>
        </form>
      )}
    </section>
  )
}

function EmployeeAmounts({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const save = useSaver()
  const elements = useElements().filter((e) => e.isActive && (e.method === 'Basic' || e.method === 'Fixed'))
  const [employeeId, setEmployeeId] = useState<number | null>(null)
  const rows = useQuery({ queryKey: ['hr', 'payEmployee', employeeId], queryFn: () => hrTime.employeePay(employeeId!), enabled: !!employeeId }).data ?? []
  const [f, setF] = useState({ payElementId: 0, amount: '', fromDate: todayHebron(), toDate: '' })
  async function submit(e: FormEvent) {
    e.preventDefault()
    if (await save(() => hrTime.addEmployeePay({ employeeId: employeeId!, payElementId: f.payElementId, amount: Number(f.amount), fromDate: f.fromDate, toDate: f.toDate || null })))
      setF((x) => ({ ...x, amount: '' }))
  }
  return (
    <section className="doc-card">
      <p className="muted">{t('hrt.paySet.employeeHint')}</p>
      <div className="form-grid"><EmployeeSelect label={t('hrt.common.employee')} value={employeeId} onChange={setEmployeeId} /></div>
      {employeeId && (
        <>
          <table className="reg-table">
            <thead><tr><th>{t('hrt.pay.element')}</th><th>{t('hrt.paySet.amount')}</th><th>{t('hrt.paySet.from')}</th><th>{t('hrt.paySet.to')}</th></tr></thead>
            <tbody>
              {rows.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {rows.map((r) => <tr key={r.id} className="reg-row"><td>{r.payElementName}</td><td dir="ltr">{money(r.amount)}</td>
                <td dir="ltr">{day(r.fromDate)}</td><td dir="ltr">{r.toDate ? day(r.toDate) : '—'}</td></tr>)}
            </tbody>
          </table>
          {canEdit && (
            <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
              <LabeledSelect label={t('hrt.pay.element')} required value={f.payElementId || ''} onChange={(e) => setF((x) => ({ ...x, payElementId: Number(e.target.value) }))}>
                <option value="" />{elements.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              </LabeledSelect>
              <LabeledInput label={t('hrt.paySet.amount')} type="number" step="0.01" min={0} dir="ltr" required value={f.amount} onChange={(e) => setF((x) => ({ ...x, amount: e.target.value }))} />
              <LabeledInput label={t('hrt.paySet.from')} type="date" dir="ltr" required value={f.fromDate} onChange={(e) => setF((x) => ({ ...x, fromDate: e.target.value }))} />
              <LabeledInput label={t('hrt.paySet.to')} type="date" dir="ltr" value={f.toDate} onChange={(e) => setF((x) => ({ ...x, toDate: e.target.value }))} />
              <div className="form-actions"><button className="btn btn-primary">{t('hr.common.add')}</button></div>
            </form>
          )}
        </>
      )}
    </section>
  )
}
