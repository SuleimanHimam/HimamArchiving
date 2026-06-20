import { useEffect, useState, useCallback, useRef } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import { documents, type DocumentListItem, DOC_STATUS_LABELS } from '../../lib/documents'
import { type PagedResult, CONFIDENTIALITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'

const STATUSES = [
  { v: '', ar: 'كل الحالات' },
  { v: '0', ar: 'مسودة' }, { v: '1', ar: 'نشطة' }, { v: '2', ar: 'مؤرشفة' },
  { v: '3', ar: 'بانتظار الإتلاف' }, { v: '4', ar: 'مُتلَفة' },
]

export default function DocumentsListPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<DocumentListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [printRows, setPrintRows] = useState<DocumentListItem[] | null>(null)
  const [printing, setPrinting] = useState(false)
  const wantPrint = useRef(false)
  const toast = useToast()

  // Print once the full register has rendered.
  useEffect(() => {
    if (wantPrint.current && printRows) { wantPrint.current = false; window.print() }
  }, [printRows])

  // Fetch every matching document (across pages) and print the register.
  async function printAll() {
    setPrinting(true)
    try {
      const all: DocumentListItem[] = []
      let p = 1
      // backend caps pageSize at 100; page through until we have them all (sane upper bound).
      for (;;) {
        const res = await documents.list({
          search: search || undefined,
          status: status === '' ? undefined : Number(status),
          page: p, pageSize: 100,
        })
        all.push(...res.items)
        if (res.items.length === 0 || all.length >= res.totalCount || all.length >= 5000) break
        p++
      }
      if (all.length === 0) { toast.error('لا توجد وثائق للطباعة'); return }
      wantPrint.current = true
      setPrintRows(all)
    } catch {
      toast.error('تعذّر تجهيز قائمة الطباعة')
    } finally { setPrinting(false) }
  }

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await documents.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      setError('تعذّر تحميل الوثائق')
    } finally { setLoading(false) }
  }, [search, status, page])

  useEffect(() => { load() }, [load])

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">DOCUMENTS · إدارة الوثائق</span>
          <h1>الوثائق</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('Documents.Print') && (
            <button className="btn btn-ghost" disabled={printing} onClick={printAll}>
              {printing ? '…جارٍ التجهيز' : '🖨 طباعة كل الوثائق'}
            </button>
          )}
          {auth.hasPermission('Documents.Create') && (
            <>
              <Link to="/app/documents/scan" className="btn btn-seal">⎙ مسح وثيقة</Link>
              <Link to="/app/documents/new" className="btn btn-primary">+ وثيقة جديدة</Link>
            </>
          )}
        </div>
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder="بحث برقم الوثيقة، العنوان، الكلمات المفتاحية، أو داخل محتوى الملفات…"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.ar}</option>)}
        </select>
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card print-hide" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>رقم الوثيقة</th><th>العنوان</th><th>النوع</th>
              <th>السرية</th><th>الإصدار</th><th>الحالة</th><th>المكان</th><th>تاريخ الانتهاء</th>
            </tr>
          </thead>
          <tbody>
            {loading && Array.from({ length: 6 }).map((_, i) => (
              <tr key={`sk${i}`} className="reg-row reg-skel">
                {Array.from({ length: 8 }).map((_, j) => <td key={j}><span className="skel-bar" /></td>)}
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={8} className="reg-empty">
                <div className="empty-state">
                  <span className="empty-state__icon" aria-hidden>▤</span>
                  <span className="empty-state__text">لا توجد وثائق مطابقة</span>
                  {auth.hasPermission('Documents.Create') && (
                    <Link to="/app/documents/new" className="btn btn-ghost btn-sm">+ إنشاء أول وثيقة</Link>
                  )}
                </div>
              </td></tr>
            )}
            {!loading && data?.items.map((d) => {
              const c = CONFIDENTIALITY_LABELS[d.confidentiality] ?? { ar: d.confidentiality, cls: 'internal' }
              return (
                <tr key={d.id} onClick={() => navigate(`/app/documents/${d.id}`)} className="reg-row">
                  <td className="mono num">{d.documentNumber}</td>
                  <td className="reg-subject">{d.title}</td>
                  <td>{d.documentTypeName}</td>
                  <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                  <td className="mono">v{d.version}</td>
                  <td><span className={`status-pill s-${d.status.toLowerCase()}`}>{DOC_STATUS_LABELS[d.status] ?? d.status}</span></td>
                  <td>{d.physicalLocationName ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'}</td>
                  <td className="mono">{d.expiryDate ?? '—'}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </motion.div>

      {data && data.totalPages > 1 && (
        <div className="pager">
          <button className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
          <span className="mono">صفحة {page} من {data.totalPages} · {data.totalCount} وثيقة</span>
          <button className="btn btn-ghost" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
        </div>
      )}

      {/* Print-only full register (populated by "طباعة كل الوثائق"). */}
      {printRows && (
        <div className="print-register">
          <div className="print-header">
            <h2>سجل الوثائق</h2>
            <div className="print-sub mono">{printRows.length} وثيقة · {new Date().toLocaleString('ar')}</div>
            <hr />
          </div>
          <table className="reg-table print-table">
            <thead>
              <tr>
                <th>رقم الوثيقة</th><th>العنوان</th><th>النوع</th>
                <th>السرية</th><th>الحالة</th><th>تاريخ الانتهاء</th>
              </tr>
            </thead>
            <tbody>
              {printRows.map((d) => (
                <tr key={d.id}>
                  <td className="mono">{d.documentNumber}</td>
                  <td>{d.title}</td>
                  <td>{d.documentTypeName}</td>
                  <td>{CONFIDENTIALITY_LABELS[d.confidentiality]?.ar ?? d.confidentiality}</td>
                  <td>{DOC_STATUS_LABELS[d.status] ?? d.status}</td>
                  <td className="mono">{d.expiryDate ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
