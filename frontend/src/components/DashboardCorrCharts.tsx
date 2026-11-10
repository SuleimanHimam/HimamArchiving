import { useTranslation } from 'react-i18next'

export interface CorrSlice { key: string; label: string; value: number; cls: string }

/** Two complementary correspondence graphs, drawn as inline SVG (no chart lib):
 *  • a donut of the direction share with a center total, and
 *  • a horizontal bar chart for magnitude comparison.
 * Colours follow the direction entity via CSS vars (theme-aware, palette validated for light+dark).
 * Every mark is directly labelled and carries a hover title, so identity is never colour-alone. */
export default function DashboardCorrCharts({ data }: { data: CorrSlice[] }) {
  const { t, i18n } = useTranslation()
  const numLocale = i18n.language === 'ar' ? 'ar-SA-u-nu-latn' : 'en-US'
  const fmt = (n: number) => n.toLocaleString(numLocale)

  const total = data.reduce((s, d) => s + d.value, 0)
  const max = Math.max(1, ...data.map((d) => d.value))

  // Donut geometry (viewBox 140×140, ring radius 54, stroke 18).
  const R = 54, SW = 18, C = 2 * Math.PI * R
  const gap = total > 0 ? 5 : 0 // circumference-unit gap between segments
  let acc = 0
  const segs = data.filter((d) => d.value > 0).map((d) => {
    const frac = d.value / total
    const len = Math.max(0.5, frac * C - gap)
    const seg = { cls: d.cls, label: d.label, value: d.value, dash: `${len} ${C - len}`, off: -acc }
    acc += frac * C
    return seg
  })

  return (
    <div className="cc">
      {/* Donut — direction share */}
      <figure className="cc__fig">
        <figcaption className="cc__cap">{t('dash.chart.share')}</figcaption>
        <div className="cc__donut-row">
          <svg viewBox="0 0 140 140" className="cc__donut" role="img" aria-label={t('dash.chart.share')}>
            <g transform="rotate(-90 70 70)">
              <circle cx="70" cy="70" r={R} fill="none" stroke="var(--brand-line)" strokeWidth={SW} opacity=".45" />
              {segs.map((s, i) => (
                <circle
                  key={i} cx="70" cy="70" r={R} fill="none" stroke={`var(--${s.cls})`} strokeWidth={SW}
                  strokeDasharray={s.dash} strokeDashoffset={s.off} strokeLinecap="butt"
                >
                  <title>{`${s.label}: ${fmt(s.value)}`}</title>
                </circle>
              ))}
            </g>
            <text x="70" y="67" textAnchor="middle" className="cc__donut-num">{fmt(total)}</text>
            <text x="70" y="86" textAnchor="middle" className="cc__donut-lbl">{t('dash.chart.total')}</text>
          </svg>
          <ul className="cc__legend">
            {data.map((d) => (
              <li key={d.key} className="cc__leg">
                <span className={`cc__dot ${d.cls}`} aria-hidden />
                <span className="cc__leg-lbl">{d.label}</span>
                <span className="cc__leg-val">{fmt(d.value)}</span>
              </li>
            ))}
          </ul>
        </div>
      </figure>

      {/* Horizontal bars — magnitude by direction */}
      <figure className="cc__fig">
        <figcaption className="cc__cap">{t('dash.chart.byDirection')}</figcaption>
        <ul className="cc__bars">
          {data.map((d) => (
            <li key={d.key} className="cc__bar-row">
              <span className="cc__bar-lbl">{d.label}</span>
              <span className="cc__bar-track">
                <span
                  className={`cc__bar-fill ${d.cls}`}
                  style={{ inlineSize: `${Math.max(d.value > 0 ? 6 : 0, (d.value / max) * 100)}%` }}
                  title={`${d.label}: ${fmt(d.value)}`}
                />
              </span>
              <span className="cc__bar-val">{fmt(d.value)}</span>
            </li>
          ))}
        </ul>
      </figure>
    </div>
  )
}
