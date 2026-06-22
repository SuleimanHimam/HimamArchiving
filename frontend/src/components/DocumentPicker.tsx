import { useEffect, useRef, useState } from 'react'
import { documents, type DocumentListItem } from '../lib/documents'

/** Autocomplete that searches documents by number/title and returns the chosen document id. */
export default function DocumentPicker({
  value, onChange, disabled, placeholder = 'ابحث برقم الوثيقة أو العنوان…',
}: {
  value: string
  onChange: (id: string) => void
  disabled?: boolean
  placeholder?: string
}) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<DocumentListItem[]>([])
  const [open, setOpen] = useState(false)
  const [label, setLabel] = useState('')
  const boxRef = useRef<HTMLDivElement>(null)

  // Resolve a display label when a value is set externally (e.g. editing an item).
  useEffect(() => {
    if (!value) { setLabel(''); return }
    let alive = true
    documents.get(Number(value))
      .then((d) => { if (alive) setLabel(`${d.documentNumber} — ${d.title}`) })
      .catch(() => { if (alive) setLabel(`#${value}`) })
    return () => { alive = false }
  }, [value])

  // Debounced search as the user types.
  useEffect(() => {
    if (!query.trim()) { setResults([]); return }
    const t = setTimeout(() => {
      documents.list({ search: query.trim(), pageSize: 8 })
        .then((r) => setResults(r.items)).catch(() => setResults([]))
    }, 250)
    return () => clearTimeout(t)
  }, [query])

  useEffect(() => {
    function onClick(e: MouseEvent) { if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  function pick(d: DocumentListItem) {
    onChange(String(d.id))
    setLabel(`${d.documentNumber} — ${d.title}`)
    setQuery(''); setOpen(false)
  }

  return (
    <div ref={boxRef} style={{ position: 'relative' }}>
      <input
        value={open ? query : label}
        disabled={disabled}
        placeholder={placeholder}
        onFocus={() => { if (!disabled) { setOpen(true); setQuery('') } }}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); if (!e.target.value) onChange('') }}
      />
      {open && results.length > 0 && (
        <ul style={{
          position: 'absolute', insetInlineStart: 0, insetInlineEnd: 0, top: 'calc(100% + 2px)', zIndex: 50,
          listStyle: 'none', margin: 0, padding: '.25rem', maxHeight: 260, overflowY: 'auto',
          background: 'var(--paper, #FBF7EC)', border: '1px solid var(--parchment-3, #E2D3B2)',
          borderRadius: 8, boxShadow: '0 8px 24px -8px rgba(20,33,61,.3)',
        }}>
          {results.map((d) => (
            <li key={d.id}
              onClick={() => pick(d)}
              style={{ padding: '.45rem .6rem', borderRadius: 6, cursor: 'pointer', fontSize: '.85rem' }}
              onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(176,137,45,.14)')}
              onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}>
              <span className="mono">{d.documentNumber}</span> — {d.title}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
