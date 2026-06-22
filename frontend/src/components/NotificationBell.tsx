import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { BellDot } from 'lucide-react'
import { type NotificationDto } from '../lib/notifications'
import {
  useNotifications, useUnreadCount, useMarkRead, useMarkAllRead,
  NOTIFICATIONS_KEY,
} from '../lib/useNotifications'
import './notificationbell.css'

function routeFor(n: NotificationDto): string | null {
  if (!n.entityType || !n.entityId) return null
  switch (n.entityType) {
    case 'Document': return `/app/documents/${n.entityId}`
    case 'IncomingMail': return `/app/incoming/${n.entityId}`
    case 'OutgoingMail': return `/app/outgoing/${n.entityId}`
    case 'Workflow': return '/app/workflow'
    case 'Destruction': return '/app/disposition'
    case 'Disposition': return '/app/disposition'
    default: return null
  }
}

export default function NotificationBell() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  const { data: unread = 0 } = useUnreadCount()
  const { data: list } = useNotifications(false)
  const markRead = useMarkRead()
  const markAll = useMarkAllRead()

  const items = list?.items ?? []
  const locale = i18n.language === 'ar' ? 'ar' : 'en'

  // Refetch full list whenever panel opens.
  function toggle() {
    const next = !open
    setOpen(next)
    if (next) qc.invalidateQueries({ queryKey: NOTIFICATIONS_KEY })
  }

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  async function onItem(n: NotificationDto) {
    if (!n.isRead) markRead.mutate(n.id)
    const route = routeFor(n)
    setOpen(false)
    if (route) navigate(route)
  }

  return (
    <div className="bell" ref={ref}>
      <button className="bell__btn" onClick={toggle} title={t('notifications.title')} aria-label={t('notifications.title')}>
        <span className="bell__icon"><BellDot size={19} strokeWidth={1.75} /></span>
        {unread > 0 && <span className="bell__badge">{unread > 99 ? '99+' : unread}</span>}
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            className="bell__panel"
            initial={{ opacity: 0, y: -8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
          >
            <div className="bell__head">
              <span>{t('notifications.title')}</span>
              {unread > 0 && (
                <button className="bell__markall" onClick={() => markAll.mutate()}>
                  {t('notifications.markAllRead')}
                </button>
              )}
            </div>
            <ul className="bell__list">
              {items.length === 0 && <li className="bell__empty">{t('notifications.empty')}</li>}
              {items.map((n) => (
                <li
                  key={n.id}
                  className={`bell__item ${n.isRead ? '' : 'is-unread'} ${n.isEscalation ? 'is-escalation' : ''}`}
                  onClick={() => onItem(n)}
                >
                  <div className="bell__title">{n.title}</div>
                  {n.body && <div className="bell__body">{n.body}</div>}
                  <div className="bell__time mono">{new Date(n.createdAt).toLocaleString(locale)}</div>
                </li>
              ))}
            </ul>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
