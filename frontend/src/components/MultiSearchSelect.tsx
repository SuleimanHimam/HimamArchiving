import { useEffect, useRef, useState } from 'react'
import { ChevronDown } from 'lucide-react'
import type { SSOption } from './SearchSelect'

/** Searchable multi-select: picked options become removable chips, the rest stay in a filtered
 * dropdown. Shares the `.ss__*` styling with SearchSelect so both read as the same control. */
export default function MultiSearchSelect({
  options, value, onChange, placeholder, ariaLabel, disabled, emptyText = 'لا نتائج مطابقة',
}: {
  options: SSOption[]
  /** Currently selected option ids. */
  value: number[]
  onChange: (ids: number[]) => void
  placeholder?: string
  ariaLabel?: string
  disabled?: boolean
  emptyText?: string
}) {
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')
  const boxRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [])

  const norm = (s: string) => s.toLocaleLowerCase()
  const picked = value
    .map((id) => options.find((o) => o.id === id))
    .filter((o): o is SSOption => !!o)

  // Already-picked entries drop out of the list so the menu only offers what can still be added.
  const available = options.filter((o) => !value.includes(o.id))
  const filtered = q.trim()
    ? available.filter((o) => norm(o.label).includes(norm(q)) || (o.sub ? norm(o.sub).includes(norm(q)) : false))
    : available

  const add = (o: SSOption) => { onChange([...value, o.id]); setQ('') }
  const remove = (id: number) => onChange(value.filter((v) => v !== id))

  return (
    <div className="ss ss--multi" ref={boxRef}>
      {picked.length > 0 && (
        <div className="ss__chips">
          {picked.map((o) => (
            <span className="ss__chip" key={o.id}>
              {o.label}
              {!disabled && (
                <button
                  type="button" className="ss__chip-x" aria-label={`إزالة ${o.label}`}
                  onClick={() => remove(o.id)}
                >×</button>
              )}
            </span>
          ))}
        </div>
      )}

      <input
        className="ss__input"
        value={q}
        placeholder={placeholder}
        aria-label={ariaLabel}
        disabled={disabled}
        autoComplete="off"
        onFocus={() => setOpen(true)}
        /* Picking an option keeps focus in the box (options preventDefault), so onFocus alone
           would never fire again — clicking the input must be able to reopen the list. */
        onClick={() => setOpen(true)}
        onChange={(e) => { setQ(e.target.value); setOpen(true) }}
        onKeyDown={(e) => {
          // Backspace on an empty box removes the last chip, as in a mail To: field.
          if (e.key === 'Backspace' && !q && picked.length) remove(picked[picked.length - 1].id)
          if (e.key === 'Enter' && filtered.length) { e.preventDefault(); add(filtered[0]) }
        }}
      />
      <ChevronDown className="ss__caret" size={13} aria-hidden />

      {open && !disabled && (
        <ul className="ss__menu" role="listbox" aria-multiselectable>
          {filtered.slice(0, 60).map((o) => (
            <li key={o.id}>
              <button
                type="button" className="ss__opt" role="option" aria-selected={false}
                onMouseDown={(e) => { e.preventDefault(); add(o) }}
              >
                <span className="ss__opt-main">
                  <span className="ss__opt-label">{o.label}</span>
                  {o.sub && <span className="ss__opt-sub">{o.sub}</span>}
                </span>
              </button>
            </li>
          ))}
          {filtered.length === 0 && (
            <li className="ss__empty">
              <span className="ss__empty-title">
                {available.length === 0 && options.length > 0 ? 'تم اختيار الكل' : emptyText}
              </span>
            </li>
          )}
        </ul>
      )}
    </div>
  )
}
