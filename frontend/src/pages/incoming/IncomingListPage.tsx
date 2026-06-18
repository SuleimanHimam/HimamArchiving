import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import {
  incomingMail, type IncomingMailListItem, type PagedResult,
  CONFIDENTIALITY_LABELS, STATUS_LABELS, PRIORITY_LABELS,
} from '../../lib/incomingMail'
import './incoming.css'

const STATUSES = [
  { v: '', ar: 'كل الحالات' },
  { v: '0', ar: 'جديدة' }, { v: '1', ar: 'محالة' }, { v: '2', ar: 'قيد المعالجة' },
  { v: '3', ar: 'معلّقة' }, { v: '4', ar: 'مغلقة' }, { v: '5', ar: 'مؤرشفة' },
]

export default function IncomingListPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<IncomingMailListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await incomingMail.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      setError('تعذّر تحميل المعاملات')
    } finally { setLoading(false) }
  }, [search, status, page])

  useEffect(() => { load() }, [load])

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">INCOMING · البريد الوارد</span>
          <h1>المعاملات الواردة</h1>
        </div>
        {auth.hasPermission('IncomingMail.Create') && (
          <Link to="/app/incoming/new" className="btn btn-seal">+ معاملة جديدة</Link>
        )}
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder="بحث برقم المعاملة أو الموضوع أو الجهة…"
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
              <th>رقم المعاملة</th><th>الجهة المرسلة</th><th>الموضوع</th>
              <th>السرية</th><th>الأولوية</th><th>الحالة</th><th>تاريخ الورود</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} className="reg-empty">…جارٍ التحميل</td></tr>}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={7} className="reg-empty">لا توجد معاملات</td></tr>
            )}
            {!loading && data?.items.map((m) => {
              const c = CONFIDENTIALITY_LABELS[m.confidentiality] ?? { ar: m.confidentiality, cls: 'internal' }
              return (
                <tr key={m.id} onClick={() => navigate(`/app/incoming/${m.id}`)} className="reg-row">
                  <td className="mono num">{m.transactionNumber}</td>
                  <td>{m.senderEntity}</td>
                  <td className="reg-subject">{m.subject}</td>
                  <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                  <td>{PRIORITY_LABELS[m.priority] ?? m.priority}</td>
                  <td><span className={`status-pill s-${m.status.toLowerCase()}`}>{STATUS_LABELS[m.status] ?? m.status}</span></td>
                  <td className="mono">{m.receivedDate}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </motion.div>

      {data && data.totalPages > 1 && (
        <div className="pager">
          <button className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>السابق</button>
          <span className="mono">صفحة {page} من {data.totalPages} · {data.totalCount} معاملة</span>
          <button className="btn btn-ghost" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>التالي</button>
        </div>
      )}
    </div>
  )
}
