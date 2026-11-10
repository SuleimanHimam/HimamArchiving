import { useState } from 'react'
import { type DashboardSummary, type StatusCount } from '../lib/reports'

/* Categorical series colours — brand-token driven so the chart tracks the active theme/skin. */
const PALETTE = [
  'var(--brand-primary, #123A5A)',
  'var(--brand-secondary, #0E7C86)',
  'var(--brand-warning, #B7791F)',
  'var(--brand-danger, #C43D3D)',
  'var(--brand-info, #2563A8)',
  'var(--brand-success, #12864F)',
]

const STATUS_AR: Record<string, string> = {
  Draft: 'مسودة', Active: 'نشطة', Archived: 'مؤرشفة', PendingDisposal: 'بانتظار التصرّف', Disposed: 'مُتلَفة',
  New: 'جديد', Assigned: 'محال', InProgress: 'قيد المعالجة', OnHold: 'معلّق', Closed: 'مغلق',
  PendingApproval: 'بانتظار الاعتماد', Approved: 'معتمد', Sent: 'مُرسل',
}
const lbl = (s: string) => STATUS_AR[s] ?? s

const TAB_LABELS: Record<string, { ar: string; en: string }> = {
  docs: { ar: 'الوثائق', en: 'Documents' },
  in: { ar: 'الوارد', en: 'Incoming' },
  out: { ar: 'الصادر', en: 'Outgoing' },
}

type Slice = { label: string; value: number; color: string }
const toSlices = (rows: StatusCount[]): Slice[] =>
  rows.map((r, i) => ({ label: lbl(r.status), value: r.count, color: PALETTE[i % PALETTE.length] }))

/** Interactive donut: hovering a slice (or legend row) highlights it and shows its value in the centre. */
function InteractiveDonut({ data }: { data: Slice[] }) {
  const [hover, setHover] = useState<number | null>(null)
  const live = data.filter((d) => d.value > 0)
  const total = live.reduce((s, d) => s + d.value, 0)
  const r = 52, c = 2 * Math.PI * r
  let acc = 0
  const segs = live.map((d, i) => {
    const seg = (d.value / (total || 1)) * c
    const o = acc; acc += seg
    return { ...d, i, seg, o }
  })

  if (total === 0) {
    return <p className="dash-panel__empty">لا توجد بيانات</p>
  }

  const center = hover != null ? live[hover] : null

  return (
    <div className="dash-donut">
      <svg viewBox="0 0 140 140" className="dash-donut__svg">
        <g transform="rotate(-90 70 70)">
          <circle cx="70" cy="70" r={r} fill="none" stroke="var(--brand-surface-2)" strokeWidth="15" />
          {segs.map((d) => (
            <circle
              key={d.i} cx="70" cy="70" r={r} fill="none" stroke={d.color}
              strokeWidth={hover === d.i ? 19 : 15}
              strokeDasharray={`${d.seg} ${c - d.seg}`} strokeDashoffset={-d.o} strokeLinecap="butt"
              style={{ opacity: hover == null || hover === d.i ? 1 : 0.3, transition: 'all .2s ease', cursor: 'pointer' }}
              onMouseEnter={() => setHover(d.i)} onMouseLeave={() => setHover(null)}
            />
          ))}
        </g>
        <text x="70" y="68" textAnchor="middle" className="dash-donut__total">{center ? center.value : total}</text>
        <text x="70" y="86" textAnchor="middle" className="dash-donut__cap">{center ? center.label : 'الإجمالي'}</text>
      </svg>
      <ul className="dash-legend">
        {live.map((d, i) => (
          <li key={i} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}
            style={{ cursor: 'pointer', opacity: hover == null || hover === i ? 1 : 0.45, transition: 'opacity .2s' }}>
            <span className="dash-legend__dot" style={{ background: d.color }} />{d.label}<b>{d.value}</b>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Status-distribution donut panel with tabbed scope (documents / incoming / outgoing). */
export default function DashboardCharts({ s }: { s: DashboardSummary }) {
  const [tab, setTab] = useState<'docs' | 'in' | 'out'>('docs')
  const data = tab === 'docs' ? s.documentsByStatus : tab === 'in' ? s.incomingByStatus : s.outgoingByStatus

  return (
    <div className="dash-panel dash-charts">
      <div className="dash-panel__head">
        <span className="dash-panel__title">التوزيع حسب الحالة</span>
        <div style={{ display: 'inline-flex', gap: 4, background: 'var(--brand-surface-2)', padding: 3, borderRadius: 'var(--brand-r-sm)' }}>
          {(['docs', 'in', 'out'] as const).map((k) => (
            <button
              key={k}
              onClick={() => setTab(k)}
              style={{
                border: 'none', cursor: 'pointer', font: 'inherit',
                padding: '5px 12px', borderRadius: 'calc(var(--brand-r-sm) - 2px)',
                fontSize: 12.5, fontWeight: 600,
                background: tab === k ? 'var(--brand-surface)' : 'transparent',
                color: tab === k ? 'var(--brand-primary)' : 'var(--brand-text-muted)',
                boxShadow: tab === k ? 'var(--brand-shadow-sm)' : 'none',
                transition: 'all var(--brand-transition)',
              }}
            >
              {TAB_LABELS[k].ar}
            </button>
          ))}
        </div>
      </div>
      <div style={{ padding: '18px' }}>
        <InteractiveDonut data={toSlices(data)} />
      </div>
    </div>
  )
}