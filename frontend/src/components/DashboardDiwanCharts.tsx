import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DiwanDashboard } from '../lib/reports'
import './dashboardDiwan.css'

/* Categorical slots 1-5 of the validated reference palette. Assigned to bureaus
   in fixed order and never cycled — a 6th bureau folds into "أخرى" (slot 5). */
const SERIES = ['s1', 's2', 's3', 's4', 's5'] as const
const MAX_BUREAUS = 4

const MONTHS_AR = ['ينا', 'فبر', 'مار', 'أبر', 'ماي', 'يون', 'يول', 'أغس', 'سبت', 'أكت', 'نوف', 'ديس']
const MONTHS_EN = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

type Direction = 'all' | 'in' | 'out'

interface Props {
  data: DiwanDashboard
  onYearChange: (year: number) => void
  /** Narrow both charts to one department. Null is the whole institution. */
  onUnitChange?: (orgUnitId: number | null) => void
}

export default function DashboardDiwanCharts({ data, onYearChange, onUnitChange }: Props) {
  const { t, i18n } = useTranslation()
  const ar = i18n.language.startsWith('ar')
  const months = ar ? MONTHS_AR : MONTHS_EN
  const [dir, setDir] = useState<Direction>('all')

  /* Offered only to someone with more than one department to choose between — for everyone else
     the charts already show their own department and the picker would be a control with one
     setting. The server decides that list, so this needs no permission check of its own. */
  const departments = data.departments ?? []
  const canFilter = !!onUnitChange && departments.length > 1
  const unitName = departments.find((d) => d.id === data.orgUnitId)?.name ?? null

  /* Bureaus ranked by volume; everything past the 4th collapses into one
     "other" bucket so the palette is never cycled. */
  const bureaus = useMemo(() => {
    const top = data.bureaus.slice(0, MAX_BUREAUS)
    const rest = data.bureaus.slice(MAX_BUREAUS)
    const list = top.map((b, i) => ({ id: b.orgUnitId, name: b.name, cls: SERIES[i] }))
    if (rest.length) {
      list.push({
        id: '__other__' as unknown as number | null,
        name: ar ? 'أخرى' : 'Other',
        cls: SERIES[MAX_BUREAUS],
      })
    }
    return list
  }, [data.bureaus, ar])

  const otherIds = useMemo(
    () => new Set(data.bureaus.slice(MAX_BUREAUS).map((b) => b.orgUnitId)),
    [data.bureaus],
  )

  const pick = (p: { incoming: number; outgoing: number }) =>
    dir === 'in' ? p.incoming : dir === 'out' ? p.outgoing : p.incoming + p.outgoing

  /* Zero-filled 12-month grid: [month][bureauIndex] = count. */
  const grid = useMemo(() => {
    const g = months.map(() => bureaus.map(() => 0))
    for (const p of data.monthlyMail) {
      const key = otherIds.has(p.orgUnitId) ? '__other__' : p.orgUnitId
      const bi = bureaus.findIndex((b) => String(b.id) === String(key))
      if (bi === -1) continue
      g[p.month - 1][bi] += pick(p)
    }
    return g
  }, [data.monthlyMail, bureaus, otherIds, months, dir])

  const totals = grid.map((row) => row.reduce((a, v) => a + v, 0))
  const mailMax = Math.max(1, ...totals)
  const attachMax = Math.max(1, ...data.monthlyAttachments.map((a) => a.count))
  const grandTotal = totals.reduce((a, v) => a + v, 0)

  return (
    <div className="ddc">
      {/* ── Mail volume by month, stacked per bureau ── */}
      <section className="ddc__card">
        <header className="ddc__head">
          <div>
            <h3 className="ddc__title">{t('dash.mailByYear', { defaultValue: ar ? 'الوارد والصادر حسب السنة' : 'Incoming & outgoing by year' })}</h3>
            <p className="ddc__sub">
              {ar ? `${grandTotal} بريد خلال ${data.year}` : `${grandTotal} items in ${data.year}`}
              {unitName && (ar ? ` · ${unitName}` : ` · ${unitName}`)}
            </p>
          </div>
          <div className="ddc__controls">
            <div className="ddc__seg" role="group" aria-label={ar ? 'الاتجاه' : 'Direction'}>
              {([
                ['all', ar ? 'الكل' : 'All'],
                ['in', ar ? 'وارد' : 'In'],
                ['out', ar ? 'صادر' : 'Out'],
              ] as const).map(([v, label]) => (
                <button key={v} type="button" className={dir === v ? 'is-on' : ''}
                  aria-pressed={dir === v} onClick={() => setDir(v)}>{label}</button>
              ))}
            </div>
            {canFilter && (
              <select className="ddc__unit" value={data.orgUnitId ?? ''}
                aria-label={ar ? 'القسم' : 'Department'}
                onChange={(e) => onUnitChange!(e.target.value ? Number(e.target.value) : null)}>
                <option value="">{ar ? 'كل الأقسام' : 'All departments'}</option>
                {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            )}
            <select className="ddc__year" value={data.year} aria-label={ar ? 'السنة' : 'Year'}
              onChange={(e) => onYearChange(Number(e.target.value))}>
              {data.availableYears.map((y) => <option key={y} value={y}>{y}</option>)}
            </select>
          </div>
        </header>

        {/* Legend — identity is never carried by colour alone. */}
        <ul className="ddc__legend">
          {bureaus.map((b) => (
            <li key={String(b.id)}><i className={`ddc__dot ddc__${b.cls}`} />{b.name}</li>
          ))}
        </ul>

        <div className="ddc__plot" role="img"
          aria-label={ar ? `حجم البريد شهريًا لسنة ${data.year}` : `Monthly mail volume for ${data.year}`}>
          {grid.map((row, mi) => (
            <div className="ddc__col" key={mi}>
              <span className="ddc__val">{totals[mi]}</span>
              <div className="ddc__stack" style={{ height: `${(totals[mi] / mailMax) * 100}%` }}>
                {row.map((v, bi) => v > 0 && (
                  <div
                    key={bi}
                    className={`ddc__seg-bar ddc__${bureaus[bi].cls}`}
                    style={{ flexGrow: v }}
                    title={`${bureaus[bi].name} — ${months[mi]}: ${v}`}
                  >
                    {/* Direct label: required relief for the low-contrast slots. */}
                    {v / mailMax > 0.08 && <span className="ddc__seg-val">{v}</span>}
                  </div>
                ))}
              </div>
              <span className="ddc__xlab">{months[mi]}</span>
            </div>
          ))}
        </div>
      </section>

      {/* ── Attachments by month ── */}
      <section className="ddc__card">
        <header className="ddc__head">
          <div>
            <h3 className="ddc__title">{t('dash.attachByMonth', { defaultValue: ar ? 'المرفقات حسب الشهر / السنة' : 'Attachments by month / year' })}</h3>
            <p className="ddc__sub">
              {ar
                ? `${data.monthlyAttachments.reduce((a, x) => a + x.count, 0)} مرفق خلال ${data.year}`
                : `${data.monthlyAttachments.reduce((a, x) => a + x.count, 0)} files in ${data.year}`}
              {unitName && ` · ${unitName}`}
            </p>
          </div>
        </header>

        <div className="ddc__plot" role="img"
          aria-label={ar ? `المرفقات شهريًا لسنة ${data.year}` : `Monthly attachments for ${data.year}`}>
          {data.monthlyAttachments.map((a) => (
            <div className="ddc__col" key={a.month}>
              <span className="ddc__val">{a.count}</span>
              <div className="ddc__stack" style={{ height: `${(a.count / attachMax) * 100}%` }}>
                {a.count > 0 && (
                  <div className="ddc__seg-bar ddc__s1" style={{ flexGrow: 1 }}
                    title={`${months[a.month - 1]}: ${a.count}`} />
                )}
              </div>
              <span className="ddc__xlab">{months[a.month - 1]}</span>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
