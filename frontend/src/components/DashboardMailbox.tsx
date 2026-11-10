import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Search, Paperclip, Circle, CircleDot } from 'lucide-react'
import { diwan } from '../lib/diwan'
import { internalMail } from '../lib/internalMail'
import { useDateFormat } from '../lib/useCalendar'
import './dashboardMailbox.css'

type Folder = 'diwan-in' | 'diwan-out' | 'internal-in' | 'internal-out'

const PAGE_SIZE = 6

/** The two mail modules carry different field names; the table reads this shape. */
interface MailRow {
  id: number
  number: string
  date: string
  subject: string
  party: string
  hasAttachments: boolean
  /** null for Diwan — external mail has no per-user read state. */
  isRead: boolean | null
}

/** Inline mailbox on the dashboard — pick a folder, search, open straight to detail. */
export default function DashboardMailbox() {
  const { i18n } = useTranslation()
  const ar = i18n.language.startsWith('ar')
  const fmt = useDateFormat()
  const [folder, setFolder] = useState<Folder>('diwan-in')
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)

  const isDiwan = folder.startsWith('diwan')

  const { data, isLoading } = useQuery<{ rows: MailRow[]; total: number }>({
    queryKey: ['dash-mailbox', folder, q, page],
    queryFn: async () => {
      const term = q.trim()
      if (isDiwan) {
        const r = term
          ? await diwan.search(term, page, PAGE_SIZE)
          : await diwan.list({
              folder: folder === 'diwan-in' ? 'incoming' : 'outgoing',
              page, pageSize: PAGE_SIZE,
            })
        return {
          total: r.totalCount,
          rows: r.items.map((m) => ({
            id: m.id,
            number: m.registerNumber || m.referenceNumber || String(m.id),
            date: m.mailDate,
            subject: m.subject,
            party: m.externalOrgName || '—',
            hasAttachments: m.hasAttachments,
            isRead: null,
          })),
        }
      }
      const r = term
        ? await internalMail.search(term, page, PAGE_SIZE)
        : await internalMail.list({
            folder: folder === 'internal-in' ? 'inbox' : 'sent',
            page, pageSize: PAGE_SIZE,
          })
      return {
        total: r.totalCount,
        rows: r.items.map((m) => ({
          id: m.id,
          number: m.mailNumber || m.referenceNumber || String(m.id),
          date: m.mailDate,
          subject: m.subject,
          party: m.senderName || '—',
          hasAttachments: m.hasAttachments,
          isRead: m.isRead,
        })),
      }
    },
  })

  const rows = data?.rows ?? []
  const total = data?.total ?? 0
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const FOLDERS: [Folder, string][] = [
    ['diwan-in', ar ? 'بريد وارد — الديوان' : 'Incoming — Diwan'],
    ['diwan-out', ar ? 'بريد صادر — الديوان' : 'Outgoing — Diwan'],
    ['internal-in', ar ? 'وارد داخلي' : 'Internal inbox'],
    ['internal-out', ar ? 'صادر داخلي' : 'Internal sent'],
  ]

  return (
    <section className="dmb">
      <header className="dmb__head">
        <select
          className="dmb__folder" value={folder}
          aria-label={ar ? 'المجلد' : 'Folder'}
          onChange={(e) => { setFolder(e.target.value as Folder); setPage(1) }}
        >
          {FOLDERS.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
        </select>

        <div className="dmb__search">
          <Search />
          <input
            value={q} placeholder={ar ? 'بحث' : 'Search'}
            aria-label={ar ? 'بحث' : 'Search'}
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
          />
        </div>
      </header>

      <div className="dmb__scroll">
        <table className="dmb__table">
          <thead>
            <tr>
              <th>{ar ? 'رقم البريد' : 'No.'}</th>
              <th>{ar ? 'التاريخ' : 'Date'}</th>
              <th>{ar ? 'الموضوع' : 'Subject'}</th>
              <th>{ar ? 'الجهة' : 'Party'}</th>
              <th className="dmb__c">{ar ? 'مرفقات' : 'Files'}</th>
              <th className="dmb__c">{ar ? 'مقروء؟' : 'Read'}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="dmb__empty">{ar ? 'جارِ التحميل…' : 'Loading…'}</td></tr>
            )}
            {!isLoading && rows.length === 0 && (
              <tr><td colSpan={6} className="dmb__empty">{ar ? 'لا توجد رسائل' : 'No messages'}</td></tr>
            )}
            {rows.map((m) => (
              <tr key={m.id}>
                <td className="dmb__num">{m.number}</td>
                <td className="dmb__num">{m.date ? fmt.date(m.date) : '—'}</td>
                <td className="dmb__subj">
                  <Link to={isDiwan ? `/app/diwan/${m.id}` : `/app/internal/${m.id}`}>
                    {m.subject || (ar ? '(بلا موضوع)' : '(no subject)')}
                  </Link>
                </td>
                <td className="dmb__party">{m.party}</td>
                <td className="dmb__c">{m.hasAttachments ? <Paperclip className="dmb__ico" /> : '—'}</td>
                <td className="dmb__c">
                  {m.isRead === null ? '—' : m.isRead
                    ? <Circle className="dmb__ico dmb__ico--read" aria-label={ar ? 'مقروء' : 'Read'} />
                    : <CircleDot className="dmb__ico dmb__ico--unread" aria-label={ar ? 'غير مقروء' : 'Unread'} />}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <footer className="dmb__foot">
        <span>{total > 0 ? `${(page - 1) * PAGE_SIZE + 1}–${Math.min(page * PAGE_SIZE, total)} ${ar ? 'من' : 'of'} ${total}` : '—'}</span>
        <div className="dmb__pager">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            {ar ? 'السابق' : 'Prev'}
          </button>
          <button type="button" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>
            {ar ? 'التالي' : 'Next'}
          </button>
        </div>
      </footer>
    </section>
  )
}
