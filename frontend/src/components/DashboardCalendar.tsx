import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { lifecycle, type ExpiringDocumentDto } from '../lib/lifecycle'

const MONTHS = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر']
// Saturday-first week (common in the region).
const WEEKDAYS = ['سبت', 'أحد', 'إثن', 'ثلا', 'أرب', 'خمي', 'جمع']
const col = (jsDay: number) => (jsDay + 1) % 7 // JS: 0=Sun..6=Sat → Sat=0..Fri=6
const pad = (n: number) => String(n).padStart(2, '0')
const key = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`

/** Dashboard month calendar: navigable, today highlighted, RTL Arabic. Days with documents whose
 * retention expires are marked; clicking a day lists those documents. */
export default function DashboardCalendar() {
  const today = new Date()
  const [view, setView] = useState(new Date(today.getFullYear(), today.getMonth(), 1))
  const [selected, setSelected] = useState<string | null>(key(today.getFullYear(), today.getMonth(), today.getDate()))
  const [events, setEvents] = useState<Record<string, ExpiringDocumentDto[]>>({})

  // Retention-expiry "events" for ~the next year (silently empty if the user can't view documents).
  useEffect(() => {
    lifecycle.expiring(365).then((list) => {
      const map: Record<string, ExpiringDocumentDto[]> = {}
      for (const e of list) (map[e.expiryDate] ??= []).push(e)
      setEvents(map)
    }).catch(() => {})
  }, [])

  const cells = useMemo(() => {
    const year = view.getFullYear(), month = view.getMonth()
    const daysInMonth = new Date(year, month + 1, 0).getDate()
    const lead = col(new Date(year, month, 1).getDay())
    const out: (number | null)[] = Array.from({ length: lead }, () => null)
    for (let d = 1; d <= daysInMonth; d++) out.push(d)
    while (out.length % 7 !== 0) out.push(null)
    return out
  }, [view])

  const isToday = (d: number) =>
    d === today.getDate() && view.getMonth() === today.getMonth() && view.getFullYear() === today.getFullYear()

  const shift = (n: number) => setView((v) => new Date(v.getFullYear(), v.getMonth() + n, 1))
  const offMonth = view.getMonth() !== today.getMonth() || view.getFullYear() !== today.getFullYear()
  const selectedEvents = selected ? events[selected] ?? [] : []

  return (
    <div className="dash-panel dash-cal">
      <div className="dash-panel__head">
        <span className="kicker">التقويم</span>
        <div className="dash-cal__nav">
          <button className="dash-cal__btn" onClick={() => shift(-1)} aria-label="الشهر السابق">‹</button>
          <span className="dash-cal__title">{MONTHS[view.getMonth()]} {view.getFullYear()}</span>
          <button className="dash-cal__btn" onClick={() => shift(1)} aria-label="الشهر التالي">›</button>
        </div>
      </div>

      <div className="dash-cal__grid">
        {WEEKDAYS.map((w) => <span key={w} className="dash-cal__wd">{w}</span>)}
        {cells.map((d, i) => {
          if (!d) return <span key={i} className="dash-cal__day is-empty" />
          const k = key(view.getFullYear(), view.getMonth(), d)
          const has = !!events[k]?.length
          return (
            <button
              key={i}
              className={`dash-cal__day ${isToday(d) ? 'is-today' : ''} ${has ? 'has-events' : ''} ${selected === k ? 'is-selected' : ''}`}
              onClick={() => setSelected(k)}
            >
              {d}
            </button>
          )
        })}
      </div>

      {offMonth && (
        <button className="dash-cal__back" onClick={() => { setView(new Date(today.getFullYear(), today.getMonth(), 1)); setSelected(key(today.getFullYear(), today.getMonth(), today.getDate())) }}>
          العودة لهذا الشهر
        </button>
      )}

      {selected && (
        <div className="dash-cal__events">
          <div className="dash-cal__events-head">
            انتهاء حفظ في {selected.split('-').reverse().join('/')}
            {selectedEvents.length > 0 && <span className="dash-cal__count">{selectedEvents.length}</span>}
          </div>
          {selectedEvents.length === 0
            ? <p className="dash-cal__none">لا توجد وثائق تنتهي مدة حفظها في هذا اليوم</p>
            : (
              <ul className="dash-cal__list">
                {selectedEvents.slice(0, 6).map((e) => (
                  <li key={e.documentId}>
                    <Link to={`/app/documents/${e.documentId}`} title={e.title}>
                      <span className="mono">{e.documentNumber}</span> — {e.title}
                    </Link>
                  </li>
                ))}
                {selectedEvents.length > 6 && <li className="dash-cal__none">+{selectedEvents.length - 6} أخرى</li>}
              </ul>
            )}
        </div>
      )}
    </div>
  )
}
