import { useEffect, useState, useCallback, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'motion/react'
import { notifications, type NotificationDto } from '../lib/notifications'
import './notificationbell.css'

// Map a notification's deep-link target to an app route.
function routeFor(n: NotificationDto): string | null {
  if (!n.entityType || !n.entityId) return null
  switch (n.entityType) {
    case 'Document': return `/app/documents/${n.entityId}`
    case 'IncomingMail': return `/app/incoming/${n.entityId}`
    case 'OutgoingMail': return `/app/outgoing/${n.entityId}`
    case 'Workflow': return '/app/workflow'
    default: return null
  }
}

export default function NotificationBell() {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<NotificationDto[]>([])
  const [unread, setUnread] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  const refresh = useCallback(async () => {
    try {
      const res = await notifications.list(false)
      setItems(res.items)
      setUnread(res.unreadCount)
    } catch { /* silent — the bell is non-critical chrome */ }
  }, [])

  // Poll the unread count periodically; load the full list when opened.
  useEffect(() => { refresh() }, [refresh])
  useEffect(() => {
    const id = setInterval(() => { notifications.unreadCount().then(setUnread).catch(() => {}) }, 30_000)
    return () => clearInterval(id)
  }, [])

  // Close on outside click.
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  async function toggle() {
    const next = !open
    setOpen(next)
    if (next) await refresh()
  }

  async function onItem(n: NotificationDto) {
    if (!n.isRead) { try { await notifications.markRead(n.id); setUnread((u) => Math.max(0, u - 1)) } catch { /* ignore */ } }
    const route = routeFor(n)
    setOpen(false)
    if (route) navigate(route)
    else refresh()
  }

  async function markAll() {
    try { await notifications.markAllRead(); setUnread(0); await refresh() } catch { /* ignore */ }
  }

  return (
    <div className="bell" ref={ref}>
      <button className="bell__btn" onClick={toggle} title="الإشعارات" aria-label="الإشعارات">
        <span className="bell__icon">🔔</span>
        {unread > 0 && <span className="bell__badge">{unread > 99 ? '99+' : unread}</span>}
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            className="bell__panel"
            initial={{ opacity: 0, y: -8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
          >
            <div className="bell__head">
              <span>الإشعارات</span>
              {unread > 0 && <button className="bell__markall" onClick={markAll}>تعليم الكل كمقروء</button>}
            </div>
            <ul className="bell__list">
              {items.length === 0 && <li className="bell__empty">لا توجد إشعارات</li>}
              {items.map((n) => (
                <li
                  key={n.id}
                  className={`bell__item ${n.isRead ? '' : 'is-unread'} ${n.isEscalation ? 'is-escalation' : ''}`}
                  onClick={() => onItem(n)}
                >
                  <div className="bell__title">{n.title}</div>
                  {n.body && <div className="bell__body">{n.body}</div>}
                  <div className="bell__time mono">{new Date(n.createdAt).toLocaleString('ar')}</div>
                </li>
              ))}
            </ul>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
