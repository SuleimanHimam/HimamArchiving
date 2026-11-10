import { X, CalendarDays } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import './datepicker.css'

const pad = (n: number) => String(n).padStart(2, '0')
const toIso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const parseIso = (s: string) => {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s || '')
  return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null
}
const sameDay = (a: Date, b: Date) =>
  a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()

/** A calendar date field: a read-only display input plus a popup month grid (Arabic month/weekday
 * names, Latin digits, RTL-aware). Emits `YYYY-MM-DD` — the same value shape as `<input type="date">`,
 * so callers are unchanged. */
export default function DatePicker({
  value, onChange, ariaLabel, disabled, clearable = true, placeholder,
}: {
  value: string
  onChange: (isoDate: string) => void
  ariaLabel?: string
  disabled?: boolean
  clearable?: boolean
  placeholder?: string
}) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language === 'ar' ? 'ar-u-nu-latn' : 'en-GB'
  const [open, setOpen] = useState(false)
  const selected = useMemo(() => parseIso(value), [value])
  const [view, setView] = useState<Date>(() => selected ?? new Date())
  const boxRef = useRef<HTMLDivElement>(null)

  // Re-centre the grid whenever an external value lands on another month.
  useEffect(() => { if (selected) setView(selected) }, [value]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDoc)
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [open])

  const monthLabel = new Intl.DateTimeFormat(locale, { month: 'long' }).format(view)
  const weekdays = useMemo(() => {
    const fmt = new Intl.DateTimeFormat(locale, { weekday: 'short' })
    // A known Sunday, then the following six days.
    return Array.from({ length: 7 }, (_, i) => fmt.format(new Date(2024, 8, 1 + i)))
  }, [locale])

  // Six-week grid including leading/trailing days from the neighbouring months.
  const cells = useMemo(() => {
    const y = view.getFullYear(), m = view.getMonth()
    const start = new Date(y, m, 1)
    start.setDate(1 - start.getDay())
    return Array.from({ length: 42 }, (_, i) => {
      const d = new Date(start)
      d.setDate(start.getDate() + i)
      return { date: d, outside: d.getMonth() !== m }
    })
  }, [view])

  const today = new Date()
  const display = selected ? `${pad(selected.getDate())}/${pad(selected.getMonth() + 1)}/${selected.getFullYear()}` : ''
  const shiftMonth = (n: number) => setView((v) => new Date(v.getFullYear(), v.getMonth() + n, 1))
  const shiftYear = (n: number) => setView((v) => new Date(v.getFullYear() + n, v.getMonth(), 1))

  function pick(d: Date) { onChange(toIso(d)); setOpen(false) }

  return (
    <div className="dpk" ref={boxRef}>
      <div className="dpk__field">
        <input
          className="dpk__input" readOnly disabled={disabled} value={display}
          aria-label={ariaLabel ?? placeholder} placeholder={placeholder ?? t('date.placeholder', { defaultValue: 'يوم/شهر/سنة' })}
          onClick={() => !disabled && setOpen((o) => !o)}
        />
        {clearable && display && !disabled && (
          <button type="button" className="dpk__clear" aria-label={t('date.clear', { defaultValue: 'مسح التاريخ' })}
            onClick={() => onChange('')}><X size={13} aria-hidden /></button>
        )}
        <button type="button" className="dpk__btn" disabled={disabled} aria-haspopup="dialog" aria-expanded={open}
          aria-label={ariaLabel ?? t('date.open', { defaultValue: 'اختيار تاريخ' })}
          onClick={() => setOpen((o) => !o)}><CalendarDays size={15} aria-hidden /></button>
      </div>

      {open && !disabled && (
        <div className="dpk__pop" role="dialog" aria-label={ariaLabel}>
          <div className="dpk__head">
            <button type="button" className="dpk__nav" aria-label={t('date.prevMonth', { defaultValue: 'الشهر السابق' })}
              onClick={() => shiftMonth(-1)}>‹</button>
            <div className="dpk__title">
              <span className="dpk__month">{monthLabel}</span>
              <span className="dpk__year">
                <button type="button" className="dpk__step" aria-label={t('date.nextYear', { defaultValue: 'السنة التالية' })}
                  onClick={() => shiftYear(1)}>▴</button>
                <span className="mono">{view.getFullYear()}</span>
                <button type="button" className="dpk__step" aria-label={t('date.prevYear', { defaultValue: 'السنة السابقة' })}
                  onClick={() => shiftYear(-1)}>▾</button>
              </span>
            </div>
            <button type="button" className="dpk__nav" aria-label={t('date.nextMonth', { defaultValue: 'الشهر التالي' })}
              onClick={() => shiftMonth(1)}>›</button>
          </div>

          <div className="dpk__grid dpk__grid--wd" aria-hidden>
            {weekdays.map((w) => <span key={w} className="dpk__wd">{w}</span>)}
          </div>

          <div className="dpk__grid" role="grid">
            {cells.map(({ date, outside }, i) => {
              const isSel = selected ? sameDay(date, selected) : false
              return (
                <button
                  key={i} type="button" role="gridcell" aria-selected={isSel}
                  className={`dpk__day ${outside ? 'is-out' : ''} ${isSel ? 'is-selected' : ''} ${sameDay(date, today) && !isSel ? 'is-today' : ''}`}
                  onClick={() => pick(date)}
                >{date.getDate()}</button>
              )
            })}
          </div>

          <div className="dpk__foot">
            <button type="button" className="dpk__todaybtn" onClick={() => pick(new Date())}>
              {t('date.today', { defaultValue: 'اليوم' })}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
