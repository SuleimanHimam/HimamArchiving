import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import { outgoingMail, type OutgoingMailListItem, OUT_STATUS_LABELS } from '../../lib/outgoingMail'
import { type PagedResult, CONFIDENTIALITY_LABELS, PRIORITY_LABELS } from '../../lib/incomingMail'
import '../incoming/incoming.css'

export default function OutgoingListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<OutgoingMailListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const STATUSES = [
    { v: '', label: t('outgoing.statuses.all') },
    { v: '0', label: t('outgoing.statuses.draft') },
    { v: '1', label: t('outgoing.statuses.pendingApproval') },
    { v: '2', label: t('outgoing.statuses.approved') },
    { v: '3', label: t('outgoing.statuses.sent') },
    { v: '4', label: t('outgoing.statuses.archived') },
  ]

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setData(await outgoingMail.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      }))
    } catch { setError(t('outgoing.loadError')) }
    finally { setLoading(false) }
  }, [search, status, page, t])

  useEffect(() => { load() }, [load])

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('outgoing.kicker')}</span>
          <h1>{t('outgoing.title')}</h1>
        </div>
        {auth.hasPermission('OutgoingMail.Create') && (
          <Link to="/app/outgoing/new" className="btn btn-seal">{t('outgoing.newButton')}</Link>
        )}
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder={t('outgoing.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
        </select>
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('outgoing.columns.number')}</th>
              <th>{t('outgoing.columns.recipient')}</th>
              <th>{t('outgoing.columns.subject')}</th>
              <th>{t('outgoing.columns.confidentiality')}</th>
              <th>{t('outgoing.columns.priority')}</th>
              <th>{t('outgoing.columns.status')}</th>
              <th>{t('outgoing.columns.date')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} className="reg-empty">{t('outgoing.loading')}</td></tr>}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={7} className="reg-empty">{t('outgoing.empty')}</td></tr>
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
          <button className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            {t('common.pagination.previous')}
          </button>
          <span className="mono">
            {t('common.pagination.page', { page, total: data.totalPages })} · {t('common.pagination.count', { count: data.totalCount })}
          </span>
          <button className="btn btn-ghost" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
            {t('common.pagination.next')}
          </button>
        </div>
      )}
    </div>
  )
}
