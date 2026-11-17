import { useTranslation } from 'react-i18next'
import { useEmployeeOptions } from '../../lib/hrtUtils'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'

/* Small pieces shared by the leave, shifts, attendance and payroll screens. */

export function EmployeeSelect({ label, value, onChange, required, allowEmpty = true, exclude }: {
  label: string; value: number | null; onChange: (v: number | null) => void; required?: boolean; allowEmpty?: boolean; exclude?: number | null
}) {
  const { t } = useTranslation()
  const employees = useEmployeeOptions()
  return (
    <LabeledSelect label={label} required={required} value={value ?? ''} onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}>
      {allowEmpty && <option value="">{t('hrt.common.pickEmployee')}</option>}
      {employees.filter((x) => x.id !== exclude).map((x) => <option key={x.id} value={x.id}>{x.employeeNumber} · {x.fullName}</option>)}
    </LabeledSelect>
  )
}

export function MonthPicker({ year, month, onChange }: { year: number; month: number; onChange: (y: number, m: number) => void }) {
  const { t } = useTranslation()
  const value = `${year}-${String(month).padStart(2, '0')}`
  return (
    <LabeledInput label={t('hrt.common.month')} type="month" dir="ltr" value={value} required
      onChange={(e) => { const [y, m] = e.target.value.split('-').map(Number); if (y && m) onChange(y, m) }} />
  )
}

export function CategoryChecks({ value, onChange }: {
  value: { appliesToMilitary: boolean; appliesToCivil: boolean; appliesToContract: boolean }
  onChange: (v: Partial<{ appliesToMilitary: boolean; appliesToCivil: boolean; appliesToContract: boolean }>) => void
}) {
  const { t } = useTranslation()
  const items = [
    ['appliesToMilitary', 'Military'], ['appliesToCivil', 'Civil'], ['appliesToContract', 'Contract'],
  ] as const
  return (
    <fieldset className="field--wide" style={{ display: 'flex', gap: '1rem', alignItems: 'center', border: 0, padding: 0 }}>
      <legend className="muted" style={{ fontSize: '.8rem' }}>{t('hrt.common.categories')}</legend>
      {items.map(([key, cat]) => (
        <label key={key} style={{ display: 'flex', gap: '.35rem', alignItems: 'center' }}>
          <input type="checkbox" checked={value[key]} onChange={(e) => onChange({ [key]: e.target.checked })} />
          {t(`hr.category.${cat}`)}
        </label>
      ))}
    </fieldset>
  )
}

export function Check({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label style={{ display: 'flex', gap: '.4rem', alignItems: 'center' }}>
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  )
}
