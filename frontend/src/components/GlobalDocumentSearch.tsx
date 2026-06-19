import { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { documents, type DocumentListItem, DOC_STATUS_LABELS } from '../lib/documents'
import './globaldocumentsearch.css'

const MIN_CHARS = 2
const DEBOUNCE_MS = 300
const MAX_RESULTS = 6

export default function GlobalDocumentSearch() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<DocumentListItem[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(false)
  const [open, setOpen] = useState(false)
  const boxRef = useRef<HTMLDivElement>(null)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const runSearch = useCallback((q: string) => {
    if (q.trim().length < MIN_CHARS) { setResults([]); setTotal(0); setLoading(false); return }
    setLoading(true)
    documents.list({ search: q.trim(), page: 1, pageSize: MAX_RESULTS })
      .then((res) => { setResults(res.items); setTotal(res.totalCount) })
      .catch(() => { setResults([]); setTotal(0) })
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => runSearch(query), DEBOUNCE_MS)
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current) }
  }, [query, runSearch])

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClickOutside)
    return () => document.removeEventListener('mousedown', onClickOutside)
  }, [])

  function goToResults() {
    const q = query.trim()
    if (!q) return
    setOpen(false)
    navigate(`/app/documents?search=${encodeURIComponent(q)}`)
  }

  function goToDoc(id: number) {
    setOpen(false)
    navigate(`/app/documents/${id}`)
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Enter') { e.preventDefault(); goToResults() }
    if (e.key === 'Escape') { setOpen(false) }
  }

  const showDropdown = open && query.trim().length >= MIN_CHARS

  return (
    <div className="gds" ref={boxRef}>
      <div className="gds__box">
        <span className="gds__icon" aria-hidden>⌕</span>
        <input
          className="gds__input"
          type="search"
          placeholder={t('documents.searchPlaceholder')}
          value={query}
          onFocus={() => setOpen(true)}
          onChange={(e) => { setQuery(e.target.value); setOpen(true) }}
          onKeyDown={onKeyDown}
        />
        {query && (
          <button
            className="gds__clear"
            type="button"
            aria-label="clear"
            onClick={() => { setQuery(''); setResults([]); setTotal(0) }}
          >✕</button>
        )}
      </div>

      <AnimatePresence>
        {showDropdown && (
          <motion.div
            className="gds__dropdown"
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.15 }}
          >
            {loading ? (
              <div className="gds__state">{t('common.loading')}</div>
            ) : results.length === 0 ? (
              <div className="gds__state">{t('documents.empty')}</div>
            ) : (
              <>
                <ul className="gds__list">
                  {results.map((d) => (
                    <li key={d.id} className="gds__item" onClick={() => goToDoc(d.id)}>
                      <span className="gds__item-title">{d.title}</span>
                      <span className="gds__item-meta">
                        <span className="mono">{d.documentNumber}</span>
                        <span className={`status-pill s-${d.status.toLowerCase()}`}>
                          {DOC_STATUS_LABELS[d.status] ?? d.status}
                        </span>
                      </span>
                    </li>
                  ))}
                </ul>
                {total > results.length && (
                  <button className="gds__more" onClick={goToResults}>
                    {t('common.pagination.count', { count: total })} — عرض الكل
                  </button>
                )}
              </>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
