import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import { outgoingMail, type OutgoingMailListItem, OUT_STATUS_LABELS } from '../../lib/outgoingMail'
import { type PagedResult, CONFIDENTIALITY_LABELS, PRIORITY_LABELS } from '../../lib/incomingMail'
import '../incoming/incoming.css'

const STATUSES = [
  { v: '', ar: 'كل الحالات' },
  { v: '0', ar: 'مسودة' }, { v: '1', ar: 'بانتظار الاعتماد' }, { v: '2', ar: 'معتمدة' },
  { v: '3', ar: 'مُرسلة' }, { v: '4', ar: 'مؤرشفة' },
]

export default function OutgoingListPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<OutgoingMailListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setData(await outgoingMail.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      }))
    } catch { setError('تعذّر تحميل الكتب الصادرة') }
    finally { setLoading(false) }
  }, [search, status, page])

  useEffect(() => { load() }, [load])

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">OUTGOING · البريد الصادر</span>
          <h1>الكتب الصادرة</h1>
        </div>
        {auth.hasPermission('OutgoingMail.Create') && (
          <Link to="/app/outgoing/new" className="btn btn-seal">+ كتاب جديد</Link>
        )}
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder="بحث برقم الكتاب أو الموضوع أو الجهة…"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.ar}</option>)}
        </select>
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>رقم الكتاب</th><th>الجهة المرسل إليها</th><th>الموضوع</th>
              <th>السرية</th><th>الأولوية</th><th>الحالة</th><th>تاريخ الإرسال</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} className="reg-empty">…جارٍ التحميل</td></tr>}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={7} className="reg-empty">لا توجد كتب صادرة</td></tr>
            )}
            {!loading && data?.items.map((m) => {
              const c = CONFIDENTIALITY_LABELS[m.confidentiality] ?? { ar: m.confidentiality, cls: 'internal' }
              return (
                <tr key={m.id} onClick={() => navigate(`/app/outgoing/${m.id}`)} className="reg-row">
                  <td className="mono num">{m.letterNumber}</td>
                  <td>{m.recipientEntity}</td>
                  <td className="reg-subject">{m.subject}</td>
                  <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                  <td>{PRIORITY_LABELS[m.priority] ?? m.priority}</td>
                  <td><span className={`status-pill s-${m.status.toLowerCase()}`}>{OUT_STATUS_LABELS[m.status] ?? m.status}</span></td>
                  <td className="mono">{m.sentDate ? m.sentDate.slice(0, 10) : '—'}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </motion.div>

      {data && data.totalPages > 1 && (
        <div className="pager">
          <button className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
          <span className="mono">صفحة {page} من {data.totalPages} · {data.totalCount} كتاب</span>
          <button className="btn btn-ghost" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
        </div>
      )}
    </div>
  )
}
