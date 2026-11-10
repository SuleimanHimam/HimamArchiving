import * as React from "react"
import { motion } from "motion/react"
import { TrendingUp, TrendingDown, type LucideIcon } from "lucide-react"
import { cn } from "@/lib/utils"

/* ────────────────────────────────────────────────────────────────────────────
   Line Graph Statistics — "Clean Wireframe Analytics"
   A self-contained, token-driven analytics card: an animated SVG line chart
   with a gradient area fill, grid lines, interactive hover tooltips, and a row
   of stat cards showing value + delta. No charting library required.
   ──────────────────────────────────────────────────────────────────────────── */

export interface LinePoint {
    /** X-axis label, e.g. "Jan" */
    label: string
    /** Numeric value plotted on the Y axis */
    value: number
}

export interface LineStat {
    label: string
    value: string
    /** Signed percentage change, e.g. +12.4 or -3.1 */
    delta: number
    icon?: LucideIcon
    /** Optional accent token name; falls back to the chart accent */
    accent?: string
}

export interface LineGraphStatisticsProps extends React.HTMLAttributes<HTMLDivElement> {
    title?: string
    description?: string
    data?: LinePoint[]
    stats?: LineStat[]
    /** Height of the plot area in px */
    height?: number
    /** Currency / unit suffix appended to hover values */
    valueSuffix?: string
    /** Time-range tabs shown in the header */
    ranges?: string[]
    defaultRange?: string
}

/* ── smooth path helpers ─────────────────────────────────────────────────── */

function buildSmoothPath(pts: { x: number; y: number }[]): string {
    if (pts.length === 0) return ""
    if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`

    const d: string[] = [`M ${pts[0].x} ${pts[0].y}`]
    for (let i = 0; i < pts.length - 1; i++) {
        const p0 = pts[i - 1] ?? pts[i]
        const p1 = pts[i]
        const p2 = pts[i + 1]
        const p3 = pts[i + 2] ?? p2
        const t = 0.18 // tension — lower = tighter, higher = looser curves
        const c1x = p1.x + (p2.x - p0.x) * t
        const c1y = p1.y + (p2.y - p0.y) * t
        const c2x = p2.x - (p3.x - p1.x) * t
        const c2y = p2.y - (p3.y - p1.y) * t
        d.push(`C ${c1x} ${c1y}, ${c2x} ${c2y}, ${p2.x} ${p2.y}`)
    }
    return d.join(" ")
}

/* ── default demo data (so <CleanWireframeAnalytics /> works standalone) ──── */

const DEFAULT_DATA: LinePoint[] = [
    { label: "Jan", value: 1240 },
    { label: "Feb", value: 1580 },
    { label: "Mar", value: 1320 },
    { label: "Apr", value: 2010 },
    { label: "May", value: 1860 },
    { label: "Jun", value: 2480 },
    { label: "Jul", value: 2240 },
    { label: "Aug", value: 2820 },
    { label: "Sep", value: 2640 },
    { label: "Oct", value: 3120 },
    { label: "Nov", value: 2960 },
    { label: "Dec", value: 3480 },
]

const DEFAULT_STATS: LineStat[] = [
    { label: "Total Volume", value: "26.7K", delta: 12.4, accent: "var(--brand-primary, #14213d)" },
    { label: "Avg / Period", value: "2.2K", delta: 4.8, accent: "var(--brand-secondary, #0E7C86)" },
    { label: "Peak Value", value: "3.48K", delta: 8.1, accent: "var(--brand-success, #2f5d3a)" },
    { label: "Lowest Dip", value: "1.24K", delta: -3.1, accent: "var(--brand-danger, #9b2226)" },
]

/* ── main component ──────────────────────────────────────────────────────── */

function LineGraphStatistics({
    title = "Analytics Overview",
    description = "Performance trends across the last 12 periods",
    data = DEFAULT_DATA,
    stats = DEFAULT_STATS,
    height = 220,
    valueSuffix = "",
    ranges = ["1M", "3M", "6M", "1Y"],
    defaultRange = "1Y",
    className,
    ...rest
}: LineGraphStatisticsProps) {
    const [range, setRange] = React.useState(defaultRange)
    const [hover, setHover] = React.useState<number | null>(null)

    /* Layout geometry */
    const W = 760
    const H = height
    const padX = 18
    const padTop = 18
    const padBottom = 28
    const plotW = W - padX * 2
    const plotH = H - padTop - padBottom

    const values = data.map((d) => d.value)
    const min = Math.min(...values)
    const max = Math.max(...values)
    const span = max - min || 1
    const niceMin = min - span * 0.12
    const niceMax = max + span * 0.12
    const niceSpan = niceMax - niceMin || 1

    const xy = data.map((d, i) => ({
        x: padX + (data.length === 1 ? plotW / 2 : (i / (data.length - 1)) * plotW),
        y: padTop + (1 - (d.value - niceMin) / niceSpan) * plotH,
    }))

    const linePath = buildSmoothPath(xy)
    const areaPath =
        xy.length > 0
            ? `${linePath} L ${xy[xy.length - 1].x} ${padTop + plotH} L ${xy[0].x} ${padTop + plotH} Z`
            : ""

    const gridLines = [0, 0.25, 0.5, 0.75, 1]

    /* unique gradient id so multiple instances don't clash */
    const gid = React.useId().replace(/:/g, "")

    return (
        <div
            className={cn(
                "w-full rounded-2xl border border-border bg-card text-card-foreground shadow-sm",
                "p-5 sm:p-6",
                className,
            )}
            {...rest}
        >
            {/* ── Header ── */}
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                    <h3 className="text-base font-bold tracking-tight text-foreground">{title}</h3>
                    {description && (
                        <p className="mt-0.5 text-[13px] text-muted-foreground">{description}</p>
                    )}
                </div>
                <div className="inline-flex rounded-lg border border-border bg-muted/50 p-0.5">
                    {ranges.map((r) => (
                        <button
                            key={r}
                            type="button"
                            onClick={() => setRange(r)}
                            className={cn(
                                "rounded-[6px] px-2.5 py-1 text-xs font-semibold transition-colors",
                                range === r
                                    ? "bg-card text-foreground shadow-sm"
                                    : "text-muted-foreground hover:text-foreground",
                            )}
                        >
                            {r}
                        </button>
                    ))}
                </div>
            </div>

            {/* ── Chart ── */}
            <div className="relative mt-5 -mx-1">
                <svg
                    viewBox={`0 0 ${W} ${H}`}
                    className="h-auto w-full"
                    preserveAspectRatio="none"
                    onMouseLeave={() => setHover(null)}
                    role="img"
                    aria-label={`${title} line graph`}
                >
                    <defs>
                        <linearGradient id={`area-${gid}`} x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="var(--brand-primary, #14213d)" stopOpacity="0.22" />
                            <stop offset="100%" stopColor="var(--brand-primary, #14213d)" stopOpacity="0" />
                        </linearGradient>
                        <linearGradient id={`line-${gid}`} x1="0" y1="0" x2="1" y2="0">
                            <stop offset="0%" stopColor="var(--brand-primary, #14213d)" />
                            <stop offset="100%" stopColor="var(--brand-secondary, #0E7C86)" />
                        </linearGradient>
                    </defs>

                    {/* Horizontal grid */}
                    {gridLines.map((g) => {
                        const y = padTop + g * plotH
                        return (
                            <line
                                key={g}
                                x1={padX}
                                y1={y}
                                x2={W - padX}
                                y2={y}
                                stroke="var(--color-border, #e6e8ec)"
                                strokeWidth={1}
                                strokeDasharray={g === 1 ? "0" : "3 4"}
                            />
                        )
                    })}

                    {/* Area fill */}
                    {areaPath && (
                        <motion.path
                            d={areaPath}
                            fill={`url(#area-${gid})`}
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            transition={{ duration: 0.8, ease: "easeOut" }}
                        />
                    )}

                    {/* Line */}
                    {linePath && (
                        <motion.path
                            d={linePath}
                            fill="none"
                            stroke={`url(#line-${gid})`}
                            strokeWidth={2.5}
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            initial={{ pathLength: 0 }}
                            animate={{ pathLength: 1 }}
                            transition={{ duration: 1.2, ease: "easeInOut" }}
                        />
                    )}

                    {/* Hover guide + active dot */}
                    {hover != null && xy[hover] && (
                        <g>
                            <line
                                x1={xy[hover].x}
                                y1={padTop}
                                x2={xy[hover].x}
                                y2={padTop + plotH}
                                stroke="var(--brand-primary, #14213d)"
                                strokeWidth={1}
                                strokeDasharray="3 3"
                                opacity={0.35}
                            />
                            <circle
                                cx={xy[hover].x}
                                cy={xy[hover].y}
                                r={6}
                                fill="var(--color-background, #fff)"
                                stroke={`url(#line-${gid})`}
                                strokeWidth={2.5}
                            />
                        </g>
                    )}

                    {/* Invisible hover columns + permanent dots */}
                    {xy.map((p, i) => (
                        <g key={i}>
                            <circle
                                cx={p.x}
                                cy={p.y}
                                r={hover === i ? 4 : 2.5}
                                fill="var(--color-background, #fff)"
                                stroke={`url(#line-${gid})`}
                                strokeWidth={2}
                                style={{ transition: "r .15s ease" }}
                            />
                            <rect
                                x={p.x - plotW / (data.length * 2)}
                                y={padTop}
                                width={plotW / data.length}
                                height={plotH}
                                fill="transparent"
                                className="cursor-pointer"
                                onMouseEnter={() => setHover(i)}
                            />
                        </g>
                    ))}

                    {/* X-axis labels */}
                    {data.map((d, i) => (
                        <text
                            key={i}
                            x={xy[i].x}
                            y={H - 6}
                            textAnchor="middle"
                            className="fill-muted-foreground"
                            style={{ fontSize: 11, fontWeight: 500, fontVariantNumeric: "tabular-nums" }}
                        >
                            {d.label}
                        </text>
                    ))}
                </svg>

                {/* Hover tooltip */}
                {hover != null && data[hover] && (
                    <div
                        className="pointer-events-none absolute -translate-x-1/2 -translate-y-full rounded-lg border border-border bg-popover px-2.5 py-1.5 text-center shadow-md"
                        style={{
                            left: `${(xy[hover].x / W) * 100}%`,
                            top: `${(xy[hover].y / H) * 100}%`,
                            marginTop: -10,
                        }}
                    >
                        <div className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                            {data[hover].label}
                        </div>
                        <div className="text-sm font-bold text-foreground">
                            {data[hover].value.toLocaleString()}
                            {valueSuffix}
                        </div>
                    </div>
                )}
            </div>

            {/* ── Stat cards ── */}
            <div className="mt-5 grid grid-cols-2 gap-3 lg:grid-cols-4">
                {stats.map((s, i) => {
                    const up = s.delta >= 0
                    const TrendIcon = s.icon ?? (up ? TrendingUp : TrendingDown)
                    return (
                        <motion.div
                            key={i}
                            initial={{ opacity: 0, y: 8 }}
                            animate={{ opacity: 1, y: 0 }}
                            transition={{ delay: 0.1 + i * 0.06, duration: 0.35 }}
                            className="rounded-xl border border-border bg-muted/30 p-3.5"
                        >
                            <div className="flex items-center justify-between">
                                <span className="text-[11.5px] font-semibold text-muted-foreground">
                                    {s.label}
                                </span>
                                <span
                                    className="grid h-6 w-6 place-items-center rounded-md"
                                    style={{
                                        background: `color-mix(in srgb, ${s.accent ?? "var(--brand-primary, #14213d)"} 14%, transparent)`,
                                        color: s.accent ?? "var(--brand-primary, #14213d)",
                                    }}
                                >
                                    <TrendIcon size={13} strokeWidth={2.5} />
                                </span>
                            </div>
                            <div className="mt-1.5 flex items-end justify-between gap-2">
                                <span
                                    className="text-xl font-extrabold tracking-tight text-foreground"
                                    style={{ fontFamily: "var(--brand-font-heading)" }}
                                >
                                    {s.value}
                                </span>
                                <span
                                    className={cn(
                                        "inline-flex items-center gap-0.5 text-[11px] font-bold",
                                        up ? "text-[var(--brand-success,#2f5d3a)]" : "text-[var(--brand-danger,#9b2226)]",
                                    )}
                                >
                                    {up ? <TrendingUp size={11} /> : <TrendingDown size={11} />}
                                    {up ? "+" : ""}
                                    {s.delta}%
                                </span>
                            </div>
                        </motion.div>
                    )
                })}
            </div>
        </div>
    )
}

export default LineGraphStatistics
export { LineGraphStatistics }