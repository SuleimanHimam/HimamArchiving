import { AnimatePresence, motion } from 'motion/react'
import { ChevronDown } from 'lucide-react'
import { useCallback, useState, type ReactNode } from 'react'

/* ── Expandable register rows ──────────────────────────────────────────────
   Adds an inline detail panel to the shared `.reg-table` register tables.

   The chevron owns the toggle and the rest of the row keeps whatever click it
   already had (usually "open this record"), so adding an expander never steals
   a navigation the user relies on. The chevron therefore stops propagation.

   Kept as a real <table>: the register tables have user-configurable columns,
   responsive column priorities (.col-opt*) and right-click context menus that
   all key off table structure, and screen readers announce row/column position
   from it. A <tr> cannot animate its own height, so the panel animates inside
   a full-width cell instead.
   ------------------------------------------------------------------------ */

/** Tracks which row is open. Only one at a time — these panels are tall. */
export function useExpandedRow<T extends string | number>() {
  const [expandedId, setExpandedId] = useState<T | null>(null)
  const toggle = useCallback(
    (id: T) => setExpandedId((current) => (current === id ? null : id)),
    [],
  )
  return { expandedId, toggle, isExpanded: (id: T) => expandedId === id }
}

/** The chevron cell. Render as the first cell of the row. */
export function ExpandCell({ expanded, onToggle }: { expanded: boolean; onToggle: () => void }) {
  return (
    <td className="reg-chev-cell">
      <button
        type="button"
        className="reg-chev"
        aria-expanded={expanded}
        aria-label={expanded ? 'طيّ التفاصيل' : 'عرض التفاصيل'}
        onClick={(e) => { e.stopPropagation(); onToggle() }}
      >
        <motion.span
          animate={{ rotate: expanded ? 180 : 0 }}
          transition={{ duration: 0.2 }}
          style={{ display: 'flex' }}
        >
          <ChevronDown size={15} />
        </motion.span>
      </button>
    </td>
  )
}

/** Matching header cell, so the chevron column has a heading slot. */
export function ExpandHeadCell() {
  return <th className="reg-chev-cell" aria-label="تفاصيل" />
}

/** The detail row itself. Always rendered so the collapse can animate out;
 *  it is zero-height and borderless while closed. */
export function ExpandedRow({ colSpan, expanded, children }: {
  colSpan: number; expanded: boolean; children: ReactNode
}) {
  return (
    <tr className="reg-detail-row">
      <td colSpan={colSpan} className="reg-detail-cell">
        <AnimatePresence initial={false}>
          {expanded && (
            <motion.div
              key="detail"
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.2 }}
              className="reg-detail"
            >
              <div className="reg-detail__inner">{children}</div>
            </motion.div>
          )}
        </AnimatePresence>
      </td>
    </tr>
  )
}

/* ── Panel building blocks ── */

export function DetailFields({ children }: { children: ReactNode }) {
  return <div className="reg-detail__fields">{children}</div>
}

export function DetailField({ label, children, wide }: {
  label: string; children: ReactNode; wide?: boolean
}) {
  return (
    <div className={`reg-detail__field${wide ? ' is-wide' : ''}`}>
      <span className="reg-detail__label">{label}</span>
      <span className="reg-detail__value">{children ?? '—'}</span>
    </div>
  )
}

export function DetailActions({ children }: { children: ReactNode }) {
  return <div className="reg-detail__actions">{children}</div>
}
