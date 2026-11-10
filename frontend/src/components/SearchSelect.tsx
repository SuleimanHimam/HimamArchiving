import { useEffect, useRef, useState } from 'react'
import { Check, ChevronDown } from 'lucide-react'

export interface SSOption { id: number; label: string; sub?: string; avatar?: string }

/** A searchable combobox: the user types to filter a list and picks an option (resolving to an id),
 * or — when `allowFreeText` — keeps whatever they typed as a plain value (id cleared). RTL-safe. */
export default function SearchSelect({
  options, value, onPick, onText, placeholder, ariaLabel, disabled,
  linked = false, linkedTitle = 'مرتبط بالسجل', showAllOnOpen = false,
}: {
  options: SSOption[]
  value: string                         // current text shown (e.g. the chosen name)
  onPick: (o: SSOption) => void         // an option was selected
  onText?: (text: string) => void       // free typing (caller usually clears the stored id)
  placeholder?: string
  ariaLabel?: string
  disabled?: boolean
  /** True when the value came from the list (an id is stored), not free text. */
  linked?: boolean
  linkedTitle?: string
  /** Open showing the whole list instead of pre-filtering by the current value.
   *  Use for short fixed vocabularies (statuses, types) where browsing beats searching. */
  showAllOnOpen?: boolean
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
  const filtered = q.trim()
    ? options.filter((o) => norm(o.label).includes(norm(q)) || (o.sub ? norm(o.sub).includes(norm(q)) : false))
    : options

  return (
    <div className="ss" ref={boxRef}>
      <input
        className="ss__input"
        value={open ? q : value}
        placeholder={placeholder}
        aria-label={ariaLabel}
        disabled={disabled}
        autoComplete="off"
        onFocus={() => { setQ(showAllOnOpen ? '' : value); setOpen(true) }}
        onClick={() => setOpen(true)}
        onChange={(e) => { setQ(e.target.value); setOpen(true); onText?.(e.target.value) }}
      />
      {/* A picked option shows a ✓ so "linked to the directory" is distinguishable from free text. */}
      {linked && !open && <span className="ss__linked" title={linkedTitle} aria-label={linkedTitle}><Check size={11} aria-hidden /></span>}
      <ChevronDown className="ss__caret" size={13} aria-hidden />
      {open && !disabled && (
        <ul className="ss__menu" role="listbox">
          {filtered.slice(0, 60).map((o) => (
            <li key={o.id}>
              <button
                type="button" className={`ss__opt${o.avatar ? ' ss__opt--avatar' : ''}`}
                onMouseDown={(e) => { e.preventDefault(); onPick(o); setOpen(false); setQ('') }}
              >
                {o.avatar && <span className="ss__opt-av" aria-hidden>{o.avatar}</span>}
                <span className="ss__opt-main">
                  <span className="ss__opt-label">{o.label}</span>
                  {o.sub && <span className="ss__opt-sub">{o.sub}</span>}
                </span>
              </button>
            </li>
          ))}
          {filtered.length === 0 && (
            <li className="ss__empty">
              <span className="ss__empty-title">لا نتائج مطابقة</span>
              {q.trim() && <span className="ss__empty-hint">سيُحفظ «{q.trim()}» كنص حر</span>}
            </li>
          )}
        </ul>
      )}
    </div>
  )
}
