import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react"
import { createPortal } from "react-dom"
import { ArrowDown, ArrowUp, ChevronDown, FilterX, Search } from "lucide-react"

import type { ColumnProps } from "./use-excel-table"

// An Excel-style AutoFilter dropdown: sort asc/desc, clear-filter, a search box, a
// (Select All) toggle, a scrollable value checklist, and OK/Cancel. Filter changes are
// STAGED locally and only committed on OK (Cancel/outside-click/Escape discards them),
// exactly like Excel. Sorting and clear-filter apply immediately.
//
// Rendered through a portal with fixed positioning so it is never clipped by the table's
// horizontal-scroll container.

const PANEL_W = 256

export function ColumnHeaderMenu({
  label, values, selected, onSelectedChange, sortDir, onSort, active,
}: ColumnProps & { label: string }) {
  const [open, setOpen] = useState(false)
  const [pos, setPos] = useState<{ top: number; left: number }>({ top: 0, left: 0 })
  const [search, setSearch] = useState("")
  const [draft, setDraft] = useState<string[] | null>(selected)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  const place = () => {
    const r = triggerRef.current?.getBoundingClientRect()
    if (!r) return
    const left = Math.min(Math.max(8, r.right - PANEL_W), window.innerWidth - PANEL_W - 8)
    setPos({ top: r.bottom + 4, left })
  }

  const openMenu = () => {
    setDraft(selected)   // re-stage from the committed value each time it opens
    setSearch("")
    place()
    setOpen(true)
  }

  useLayoutEffect(() => { if (open) place() }, [open])

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => {
      if (panelRef.current?.contains(e.target as Node) || triggerRef.current?.contains(e.target as Node)) return
      setOpen(false) // outside click discards the draft
    }
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false) }
    const onReflow = () => place()
    document.addEventListener("mousedown", onDoc)
    document.addEventListener("keydown", onKey)
    window.addEventListener("resize", onReflow)
    window.addEventListener("scroll", onReflow, true)
    return () => {
      document.removeEventListener("mousedown", onDoc)
      document.removeEventListener("keydown", onKey)
      window.removeEventListener("resize", onReflow)
      window.removeEventListener("scroll", onReflow, true)
    }
  }, [open])

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    return q ? values.filter((v) => v.toLowerCase().includes(q)) : values
  }, [values, search])

  const isChecked = (v: string) => draft === null || draft.includes(v)
  const toggle = (v: string) => {
    const cur = draft ?? values
    const next = cur.includes(v) ? cur.filter((x) => x !== v) : [...cur, v]
    setDraft(next.length === values.length ? null : next)
  }
  const allVisibleChecked = visible.length > 0 && visible.every(isChecked)
  const toggleAll = () => {
    const cur = new Set(draft ?? values)
    if (allVisibleChecked) visible.forEach((v) => cur.delete(v))
    else visible.forEach((v) => cur.add(v))
    setDraft(cur.size === values.length ? null : Array.from(cur))
  }

  const applySort = (dir: "asc" | "desc") => { onSort(dir); setOpen(false) }
  const clearFilter = () => { onSelectedChange(null); setOpen(false) }
  const commit = () => { onSelectedChange(draft); setOpen(false) }

  const itemCls = "flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-start text-sm transition-colors hover:bg-accent hover:text-accent-foreground disabled:pointer-events-none disabled:opacity-40"

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="col-sort inline-flex cursor-pointer items-center gap-1 whitespace-nowrap border-0 bg-transparent p-0 text-inherit [font:inherit] hover:opacity-80"
        aria-label={label}
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={() => (open ? setOpen(false) : openMenu())}
      >
        <span>{label}</span>
        {sortDir === "asc" && <ArrowUp className="h-3 w-3" />}
        {sortDir === "desc" && <ArrowDown className="h-3 w-3" />}
        {active && <span className="inline-block h-1.5 w-1.5 rounded-full bg-primary" aria-hidden />}
        <ChevronDown className="h-3 w-3 opacity-50" />
      </button>

      {open && createPortal(
        <div
          ref={panelRef}
          role="dialog"
          style={{ position: "fixed", top: pos.top, left: pos.left, width: PANEL_W }}
          className="z-[60] overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-lg"
        >
          <div className="p-1">
            <button type="button" className={itemCls} onClick={() => applySort("asc")}>
              <ArrowUp className="h-4 w-4 opacity-60" /> ترتيب تصاعدي
            </button>
            <button type="button" className={itemCls} onClick={() => applySort("desc")}>
              <ArrowDown className="h-4 w-4 opacity-60" /> ترتيب تنازلي
            </button>
            <button type="button" className={itemCls} disabled={!active} onClick={clearFilter}>
              <FilterX className="h-4 w-4 opacity-60" /> مسح التصفية
            </button>
          </div>

          <div className="border-t p-2">
            <div className="relative">
              <Search className="pointer-events-none absolute start-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <input
                autoFocus
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="بحث"
                aria-label="بحث"
                className="h-8 w-full rounded-md border border-input bg-transparent ps-7 pe-2 text-sm outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>

            <div className="mt-2 max-h-52 overflow-y-auto rounded-md border">
              <label className="flex cursor-pointer items-center gap-2 px-2 py-1.5 text-sm font-medium hover:bg-accent">
                <input type="checkbox" checked={allVisibleChecked} onChange={toggleAll} className="accent-primary" />
                <span>(تحديد الكل)</span>
              </label>
              {visible.map((v) => (
                <label key={v} className="flex cursor-pointer items-center gap-2 px-2 py-1.5 text-sm hover:bg-accent">
                  <input type="checkbox" checked={isChecked(v)} onChange={() => toggle(v)} className="accent-primary" />
                  <span className="truncate">{v}</span>
                </label>
              ))}
              {visible.length === 0 && <p className="px-2 py-3 text-center text-sm text-muted-foreground">لا نتائج</p>}
            </div>
          </div>

          <div className="flex justify-end gap-2 border-t p-2">
            <button type="button" onClick={commit} className="rounded-md bg-primary px-4 py-1 text-xs font-medium text-primary-foreground hover:bg-primary/90">
              موافق
            </button>
            <button type="button" onClick={() => setOpen(false)} className="rounded-md border px-4 py-1 text-xs hover:bg-accent">
              إلغاء
            </button>
          </div>
        </div>,
        document.body,
      )}
    </>
  )
}
