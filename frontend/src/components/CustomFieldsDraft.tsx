import { optionList } from '../lib/customFields'
import { type CustomFieldsDraft } from '../lib/useCustomFieldsDraft'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'

/**
 * The inputs for a record's custom fields while it is still being created — the visible half of
 * useCustomFieldsDraft. Renders nothing when the chosen document type adds no fields of its own.
 */
export default function CustomFieldsFieldset(
  { draft, title = 'حقول مخصصة' }: { draft: CustomFieldsDraft; title?: string },
) {
  if (!draft.hasFields) return null
  return (
    <>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: '.5rem 0 0' }}>{title}</h4>
      {draft.defs.map((f) => {
        const v = draft.values[f.id] ?? ''
        if (f.fieldType === 1) return <LabeledInput key={f.id} label={f.label} type="number" dir="ltr" value={v} onChange={(e) => draft.set(f.id, e.target.value)} />
        if (f.fieldType === 2) return <LabeledInput key={f.id} label={f.label} type="date" dir="ltr" value={v} onChange={(e) => draft.set(f.id, e.target.value)} />
        if (f.fieldType === 3) return (
          <LabeledSelect key={f.id} label={f.label} value={v} onChange={(e) => draft.set(f.id, e.target.value)}>
            <option value="">—</option>
            {optionList(f).map((o) => <option key={o} value={o}>{o}</option>)}
          </LabeledSelect>
        )
        return <LabeledInput key={f.id} label={f.label} value={v} onChange={(e) => draft.set(f.id, e.target.value)} />
      })}
    </>
  )
}
