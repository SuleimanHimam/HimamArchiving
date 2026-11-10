import * as React from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Search, Filter, Check } from 'lucide-react'

import { Input } from './input'
import { Button } from './button'

// A single-select chip filter modelled on the audit-log (MonitoringPage) sidebar, made reusable
// so mail lists and the physical archive share the exact same interaction. Each page supplies its
// own groups — its "advantage" — while the search box + collapsible panel behave identically.

export interface ChipOption {
  value: string
  label: string
  color?: string   // optional leading dot (e.g. an action/status colour)
  depth?: number   // optional indent for hierarchical options (e.g. location paths)
}

export interface ChipGroup {
  key: string
  label: string
  options: ChipOption[]
  selected: string                      // '' = nothing selected
  onSelect: (value: string) => void     // called with the new value ('' when toggled off)
}

function chipClass(selected: boolean) {
  return `flex w-full items-center justify-between gap-2 rounded-md border px-3 py-2 text-start text-sm transition-colors ${
    selected
      ? 'border-primary bg-primary/10 text-primary'
      : 'border-border text-muted-foreground hover:border-primary/40 hover:bg-muted/40'
  }`
}

/** Live-search input + a filter-toggle button that shows a count badge of active filters. */
export function FilterSearchRow({
  search, onSearch, placeholder, showFilters, onToggleFilters, activeCount, children,
}: {
  search: string
  onSearch: (value: string) => void
  placeholder: string
  showFilters: boolean
  onToggleFilters: () => void
  activeCount: number
  children?: React.ReactNode
}) {
  return (
    <div className="flex items-center gap-2">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder={placeholder}
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          className="h-9 ps-9 text-sm"
        />
      </div>
      <Button
        type="button"
        variant={showFilters ? 'default' : 'outline'}
        size="sm"
        className="relative"
        onClick={onToggleFilters}
      >
        <Filter className="h-4 w-4" />
        {activeCount > 0 && (
          <span className="absolute -end-2 -top-2 flex h-5 w-5 items-center justify-center rounded-full bg-destructive text-[10px] font-semibold text-destructive-foreground">
            {activeCount}
          </span>
        )}
      </Button>
      {children}
    </div>
  )
}

/** The animated, collapsible left panel of single-select chip groups. */
export function FilterSidebar({
  open, groups, panelLabel, clearLabel, hasActive, onClear, extra, width = 260,
}: {
  open: boolean
  groups: ChipGroup[]
  panelLabel: string
  clearLabel: string
  hasActive?: boolean
  onClear?: () => void
  extra?: React.ReactNode
  width?: number
}) {
  const showClear = (hasActive ?? groups.some((g) => g.selected !== '')) && !!onClear

  return (
    <AnimatePresence initial={false}>
      {open && (
        <motion.div
          key="filter-sidebar"
          initial={{ width: 0, opacity: 0 }}
          animate={{ width, opacity: 1 }}
          exit={{ width: 0, opacity: 0 }}
          transition={{ duration: 0.2 }}
          className="shrink-0 self-stretch overflow-hidden rounded-lg border border-border"
        >
          {/* Fixed inner width so the content doesn't reflow/squish while the panel animates. */}
          <div className="flex h-full flex-col gap-6 overflow-y-auto bg-card p-4" style={{ width }}>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-foreground">{panelLabel}</h3>
              {showClear && (
                <Button variant="ghost" size="sm" className="h-6 text-xs" onClick={onClear}>
                  {clearLabel}
                </Button>
              )}
            </div>

            {groups.map((group) => (
              <div key={group.key} className="space-y-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.label}</p>
                <div className="space-y-2">
                  {group.options.map((opt) => {
                    const selected = group.selected === opt.value
                    return (
                      <button
                        key={opt.value}
                        type="button"
                        aria-pressed={selected}
                        onClick={() => group.onSelect(selected ? '' : opt.value)}
                        className={chipClass(selected)}
                        style={opt.depth ? { paddingInlineStart: `${0.75 + opt.depth * 0.75}rem` } : undefined}
                      >
                        <span className="flex items-center gap-2 truncate">
                          {opt.color && <span className="h-2 w-2 shrink-0 rounded-full" style={{ backgroundColor: opt.color }} />}
                          <span className="truncate">{opt.label}</span>
                        </span>
                        {selected && <Check className="h-3.5 w-3.5 shrink-0" />}
                      </button>
                    )
                  })}
                </div>
              </div>
            ))}

            {extra}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
