import {
    useCallback, useEffect, useId, useMemo, useRef, useState,
    type CSSProperties, type HTMLAttributes, type MouseEvent, type ReactNode,
} from 'react'
import { motion, useSpring, useTransform, AnimatePresence } from 'motion/react'
import {
    ArrowUp, ArrowDown, Minus, ChevronDown, type LucideIcon,
} from 'lucide-react'
import { withLatnDigits } from '../lib/datetime'
import './modern-card.css'

/* ============================================================================
   أوراق · Archevo — Modern Interactive Cards
   A reusable, motion-rich card kit. Built on the brand token vocabulary and
   the modern elevation scale. RTL-safe, theme-aware, reduced-motion friendly.

   Exports:
     • ModernCard          — base surface with variants (glass / glow / gradient / tilt)
     • StatCard            — KPI metric (count-up + trend + sparkline + progress)
     • ActionCard          — media header + hover-reveal action buttons
     • ProgressCard        — animated ring + legend
     • ExpandableCard      — collapsible accordion-style card
     • CardGrid            — responsive grid helper
   ========================================================================== */

export type CardTone = 'primary' | 'accent' | 'success' | 'warning' | 'danger' | 'info'

/* ── hooks ────────────────────────────────────────────────────────────── */

/** Animated count-up; respects prefers-reduced-motion. */
export function useCountUp(target: number, duration = 900) {
    const [n, setN] = useState(0)
    const reduced = usePrefersReducedMotion()
    useEffect(() => {
        if (target === 0) { setN(0); return }
        if (reduced) { setN(target); return }
        let raf = 0
        const start = performance.now()
        const tick = (now: number) => {
            const p = Math.min(1, (now - start) / duration)
            // easeOutCubic
            const eased = 1 - Math.pow(1 - p, 3)
            setN(Math.round(eased * target))
            if (p < 1) raf = requestAnimationFrame(tick)
        }
        raf = requestAnimationFrame(tick)
        return () => cancelAnimationFrame(raf)
    }, [target, duration, reduced])
    return n
}

function usePrefersReducedMotion() {
    const [reduced, setReduced] = useState(false)
    useEffect(() => {
        const mq = window.matchMedia('(prefers-reduced-motion: reduce)')
        const update = () => setReduced(mq.matches)
        update()
        mq.addEventListener('change', update)
        return () => mq.removeEventListener('change', update)
    }, [])
    return reduced
}

/** 3D tilt + pointer-tracked CSS vars (--mx/--my) for glow/shine. */
function useTilt(max = 8) {
    const ref = useRef<HTMLDivElement>(null)
    const reduced = usePrefersReducedMotion()
    const rx = useSpring(0, { stiffness: 200, damping: 18 })
    const ry = useSpring(0, { stiffness: 200, damping: 18 })
    const rotateX = useTransform(rx, (v) => `${v}deg`)
    const rotateY = useTransform(ry, (v) => `${v}deg`)

    const onMove = useCallback((e: MouseEvent<HTMLDivElement>) => {
        const el = ref.current
        if (!el || reduced) return
        const r = el.getBoundingClientRect()
        const px = (e.clientX - r.left) / r.width
        const py = (e.clientY - r.top) / r.height
        el.style.setProperty('--mx', `${(px * 100).toFixed(1)}%`)
        el.style.setProperty('--my', `${(py * 100).toFixed(1)}%`)
        // Invert so the card tilts toward the pointer.
        ry.set((px - 0.5) * max * 2)
        rx.set(-(py - 0.5) * max * 2)
    }, [max, reduced, rx, ry])

    const onLeave = useCallback(() => {
        rx.set(0); ry.set(0)
    }, [rx, ry])

    return { ref, rotateX, rotateY, onMove, onLeave, enabled: !reduced }
}

/* ── base card ────────────────────────────────────────────────────────── */

export interface ModernCardProps extends HTMLAttributes<HTMLDivElement> {
    variant?: 'solid' | 'glass' | 'glow' | 'gradient'
    /** Add the 3D tilt + pointer-tracking shine. */
    tilt?: boolean
    /** Lift on hover + pointer cursor. */
    interactive?: boolean
    tone?: CardTone
    /** Override the glow color (CSS color). Defaults to tone/primary. */
    glowColor?: string
    as?: 'div' | 'article' | 'section'
}

export function ModernCard({
    variant = 'solid',
    tilt = false,
    interactive = false,
    tone,
    glowColor,
    className = '',
    children,
    style,
    as = 'div',
    ...rest
}: ModernCardProps) {
    const tiltState = useTilt(8)
    const Tag = as
    const classes = [
        'mc',
        variant === 'glass' && 'mc--glass',
        variant === 'glow' && 'mc--glow',
        variant === 'gradient' && 'mc--gradient',
        tilt && 'mc--tilt',
        interactive && 'mc--interactive',
        tone && `mc--tone-${tone}`,
        className,
    ].filter(Boolean).join(' ')

    const cssVars: CSSProperties = {
        ...(glowColor ? ({ ['--mc-glow' as string]: glowColor } as CSSProperties) : {}),
        ...style,
    }

    // Tilt wraps in a perspective scene + motion.div.
    if (tilt) {
        return (
            <div className="mc-tilt-scene" style={cssVars}>
                <motion.div
                    ref={tiltState.ref}
                    className={classes}
                    style={{ rotateX: tiltState.rotateX, rotateY: tiltState.rotateY, transformStyle: 'preserve-3d' }}
                    onMouseMove={tiltState.onMove}
                    onMouseLeave={tiltState.onLeave}
                    {...(rest as object)}
                >
                    {variant === 'glow' || variant === 'gradient' ? <span className="mc__shine" /> : null}
                    <div className="mc__content">{children}</div>
                </motion.div>
            </div>
        )
    }

    return (
        <Tag className={classes} style={cssVars} {...rest}>
            {(variant === 'glow' || variant === 'gradient') && <span className="mc__shine" />}
            {children}
        </Tag>
    )
}

/* ── stat card ────────────────────────────────────────────────────────── */

export interface StatCardProps {
    label: string
    value: number
    /** Sub-text under the label. */
    sublabel?: string
    icon?: LucideIcon
    tone?: CardTone
    /** Trend pill. */
    trend?: { dir: 'up' | 'down' | 'flat'; label: string }
    /** 0–100; draws a slim progress bar at the bottom. */
    progress?: number
    /** Sparkline series; first point is the oldest. */
    spark?: number[]
    /** Locale string for number formatting (e.g. 'ar-SA', 'en-US'). */
    locale?: string
    /** Format the displayed value (overrides count-up). */
    format?: (n: number) => string
    interactive?: boolean
    onClick?: () => void
    className?: string
}

export function StatCard({
    label, value, sublabel, icon: Icon, tone = 'primary',
    trend, progress, spark, locale = 'en-US', format,
    interactive = true, onClick, className = '',
}: StatCardProps) {
    const n = useCountUp(value)
    const display = format ? format(value) : n.toLocaleString(withLatnDigits(locale))
    const gradId = useId().replace(/:/g, '')
    const reduced = usePrefersReducedMotion()

    const sparkPath = useMemo(() => {
        if (!spark || spark.length < 2) return null
        const w = 100, h = 36, pad = 3
        const min = Math.min(...spark), max = Math.max(...spark)
        const range = max - min || 1
        const step = (w - pad * 2) / (spark.length - 1)
        const pts = spark.map((v, i) => {
            const x = pad + i * step
            const y = pad + (h - pad * 2) * (1 - (v - min) / range)
            return [x, y] as const
        })
        const line = pts.map((p, i) => `${i === 0 ? 'M' : 'L'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ')
        const area = `${line} L${pts[pts.length - 1][0].toFixed(1)},${h} L${pts[0][0].toFixed(1)},${h} Z`
        return { line, area }
    }, [spark])

    return (
        <ModernCard
            as="article"
            interactive={interactive}
            tone={tone}
            variant="glow"
            tilt={interactive && !reduced}
            onClick={onClick}
            className={className}
            style={{ cursor: onClick ? 'pointer' : undefined }}
        >
            <div className="mc__body">
                <div className="mc-stat__top">
                    {Icon && (
                        <span className="mc-stat__icon">
                            <Icon size={22} strokeWidth={2} />
                        </span>
                    )}
                    {trend && (
                        <span className={`mc-stat__trend is-${trend.dir}`}>
                            {trend.dir === 'up' ? <ArrowUp size={11} /> : trend.dir === 'down' ? <ArrowDown size={11} /> : <Minus size={11} />}
                            {trend.label}
                        </span>
                    )}
                </div>
                <div className="mc-stat__value">{display}</div>
                <div className="mc-stat__label">{label}</div>
                {sublabel && <div className="mc-stat__sub">{sublabel}</div>}

                {sparkPath && (
                    <svg className="mc-spark" viewBox="0 0 100 36" preserveAspectRatio="none" aria-hidden>
                        <defs>
                            <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
                                <stop offset="0%" stopColor="var(--mc-accent, var(--brand-primary))" stopOpacity="0.35" />
                                <stop offset="100%" stopColor="var(--mc-accent, var(--brand-primary))" stopOpacity="0" />
                            </linearGradient>
                        </defs>
                        <path className="mc-spark__area" d={sparkPath.area} style={{ fill: `url(#${gradId})` }} />
                        <path className="mc-spark__line" d={sparkPath.line} />
                    </svg>
                )}

                {typeof progress === 'number' && (
                    <div className="mc-stat__progress" title={`${Math.round(progress)}%`}>
                        <motion.div
                            className="mc-stat__progress-bar"
                            initial={{ width: 0 }}
                            animate={{ width: `${Math.max(0, Math.min(100, progress))}%` }}
                            transition={{ duration: 1, ease: [0.22, 1, 0.36, 1] }}
                        />
                    </div>
                )}
            </div>
        </ModernCard>
    )
}

/* ── action card ──────────────────────────────────────────────────────── */

export interface ActionCardProps {
    title: string
    subtitle?: string
    icon?: LucideIcon
    tone?: CardTone
    actions?: { icon: LucideIcon; label: string; onClick: () => void }[]
    onClick?: () => void
    className?: string
    children?: ReactNode
}

export function ActionCard({
    title, subtitle, icon: Icon, tone = 'primary', actions = [], onClick, className = '', children,
}: ActionCardProps) {
    const reduced = usePrefersReducedMotion()
    return (
        <ModernCard
            as="article"
            interactive
            tone={tone}
            tilt={!reduced}
            onClick={onClick}
            className={className}
            style={{ cursor: onClick ? 'pointer' : undefined }}
        >
            <div className="mc-action__media">
                {Icon && <Icon className="mc-action__media-icon" size={46} strokeWidth={1.6} />}
                {actions.length > 0 && (
                    <div className="mc-action__overlay">
                        {actions.map((a, i) => (
                            <button
                                key={i}
                                className="mc-action__btn"
                                title={a.label}
                                aria-label={a.label}
                                onClick={(e) => { e.stopPropagation(); a.onClick() }}
                            >
                                <a.icon size={17} />
                            </button>
                        ))}
                    </div>
                )}
            </div>
            <div className="mc__body">
                <h3 className="mc-action__title">{title}</h3>
                {subtitle && <p className="mc-action__sub">{subtitle}</p>}
                {children}
            </div>
        </ModernCard>
    )
}

/* ── progress / ring card ─────────────────────────────────────────────── */

export interface ProgressCardProps {
    label: string
    /** 0–100. */
    value: number
    caption?: string
    tone?: CardTone
    legend?: { label: string; value: number; color?: string }[]
    className?: string
    interactive?: boolean
}

export function ProgressCard({
    label, value, caption, tone = 'primary', legend = [], className = '', interactive = false,
}: ProgressCardProps) {
    const v = Math.max(0, Math.min(100, value))
    const r = 38, c = 2 * Math.PI * r
    const shown = useCountUp(v, 1100)
    const offset = c - (shown / 100) * c

    return (
        <ModernCard as="article" tone={tone} interactive={interactive} className={className}>
            <div className="mc__body">
                <div className="mc-progress__row">
                    <div className="mc-ring">
                        <svg className="mc-ring__svg" width="92" height="92" viewBox="0 0 92 92">
                            <circle className="mc-ring__track" cx="46" cy="46" r={r} />
                            <motion.circle
                                className="mc-ring__fill"
                                cx="46" cy="46" r={r}
                                strokeDasharray={c}
                                initial={{ strokeDashoffset: c }}
                                animate={{ strokeDashoffset: offset }}
                                transition={{ duration: 1.1, ease: [0.22, 1, 0.36, 1] }}
                            />
                        </svg>
                        <div className="mc-ring__center">{Math.round(shown)}%</div>
                    </div>
                    <div>
                        <div className="mc-progress__label">{label}</div>
                        <div className="mc-progress__value">{caption ?? `${Math.round(v)}%`}</div>
                    </div>
                </div>
                {legend.length > 0 && (
                    <ul className="mc-progress__legend">
                        {legend.map((l, i) => (
                            <li key={i}>
                                <span
                                    className="mc-progress__dot"
                                    style={{ background: l.color ?? 'var(--mc-accent, var(--brand-primary))' }}
                                />
                                {l.label}
                                <b>{l.value}</b>
                            </li>
                        ))}
                    </ul>
                )}
            </div>
        </ModernCard>
    )
}

/* ── expandable card ──────────────────────────────────────────────────── */

export interface ExpandableCardProps {
    title: ReactNode
    subtitle?: ReactNode
    defaultOpen?: boolean
    icon?: LucideIcon
    tone?: CardTone
    className?: string
    children: ReactNode
}

export function ExpandableCard({
    title, subtitle, defaultOpen = false, icon: Icon, tone, className = '', children,
}: ExpandableCardProps) {
    const [open, setOpen] = useState(defaultOpen)
    return (
        <ModernCard as="section" tone={tone} className={`mc-expand ${open ? 'is-open' : ''} ${className}`}>
            <div
                className="mc-expand__head"
                role="button"
                tabIndex={0}
                aria-expanded={open}
                onClick={() => setOpen((o) => !o)}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOpen((o) => !o) } }}
            >
                <div style={{ display: 'flex', alignItems: 'center', gap: 12, minWidth: 0 }}>
                    {Icon && (
                        <span className="mc-stat__icon" style={{ width: 38, height: 38 }}>
                            <Icon size={18} />
                        </span>
                    )}
                    <div style={{ minWidth: 0 }}>
                        <div className="mc-action__title">{title}</div>
                        {subtitle && <div className="mc-action__sub">{subtitle}</div>}
                    </div>
                </div>
                <span className="mc-expand__chevron"><ChevronDown size={20} /></span>
            </div>
            <AnimatePresence initial={false}>
                {open && (
                    <motion.div
                        className="mc-expand__content"
                        initial={{ height: 0, opacity: 0 }}
                        animate={{ height: 'auto', opacity: 1 }}
                        exit={{ height: 0, opacity: 0 }}
                        transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
                    >
                        <div className="mc__body" style={{ paddingTop: 0 }}>{children}</div>
                    </motion.div>
                )}
            </AnimatePresence>
        </ModernCard>
    )
}

/* ── grid helper ──────────────────────────────────────────────────────── */

export interface CardGridProps extends HTMLAttributes<HTMLDivElement> {
    cols?: 2 | 3 | 4 | 'auto'
}

export function CardGrid({ cols = 'auto', className = '', style, children, ...rest }: CardGridProps) {
    return (
        <div className={`mc-grid mc-grid--${cols} ${className}`} style={style} {...rest}>
            {children}
        </div>
    )
}