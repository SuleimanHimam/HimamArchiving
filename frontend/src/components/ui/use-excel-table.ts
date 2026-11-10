import { useMemo, useState } from "react"

// Excel-style AutoFilter state for a client-side (already-loaded) table page.
// Filtering and sorting run over the rows passed in — for a server-paginated list that
// means the current page, which is the honest scope here.

export type SortDir = "asc" | "desc"

export interface ColumnDef<T> {
  key: string
  /** The display string used for the value checklist and (by default) sorting. */
  accessor: (row: T) => string
  /** Optional comparable used for sorting instead of the display string. */
  sortValue?: (row: T) => string | number
}

export interface ColumnProps {
  values: string[]
  selected: string[] | null            // null = all values shown
  onSelectedChange: (next: string[] | null) => void
  sortDir: SortDir | null
  onSort: (dir: SortDir | null) => void
  active: boolean
}

const EMPTY = "—" // em dash shown for blank cells

export function useExcelTable<T>(rows: T[], columns: ColumnDef<T>[]) {
  const [sort, setSort] = useState<{ key: string; dir: SortDir } | null>(null)
  const [filters, setFilters] = useState<Record<string, string[] | null>>({})

  const colMap = useMemo(
    () => Object.fromEntries(columns.map((c) => [c.key, c])) as Record<string, ColumnDef<T>>,
    [columns],
  )

  // Distinct values per column, taken from the whole loaded set so the checklist stays
  // stable regardless of which columns are already filtered.
  const valuesByCol = useMemo(() => {
    const out: Record<string, string[]> = {}
    for (const c of columns) {
      const set = new Set<string>()
      for (const r of rows) set.add(c.accessor(r) || EMPTY)
      out[c.key] = Array.from(set).sort((a, b) => a.localeCompare(b, "ar"))
    }
    return out
  }, [rows, columns])

  const viewRows = useMemo(() => {
    let out = rows.filter((r) =>
      columns.every((c) => {
        const sel = filters[c.key]
        if (!sel) return true // null/undefined = all shown
        return sel.includes(c.accessor(r) || EMPTY)
      }),
    )
    if (sort) {
      const c = colMap[sort.key]
      if (c) {
        const val = c.sortValue ?? c.accessor
        out = [...out].sort((a, b) => {
          const av = val(a), bv = val(b)
          const cmp = typeof av === "number" && typeof bv === "number"
            ? av - bv
            : String(av).localeCompare(String(bv), "ar")
          return sort.dir === "asc" ? cmp : -cmp
        })
      }
    }
    return out
  }, [rows, columns, filters, sort, colMap])

  const getColumnProps = (key: string): ColumnProps => ({
    values: valuesByCol[key] ?? [],
    selected: filters[key] ?? null,
    onSelectedChange: (next) => setFilters((f) => ({ ...f, [key]: next })),
    sortDir: sort?.key === key ? sort.dir : null,
    onSort: (dir) => setSort(dir ? { key, dir } : null),
    active: !!filters[key],
  })

  const anyActive = sort !== null || Object.values(filters).some((v) => v != null)
  const reset = () => { setSort(null); setFilters({}) }

  return { viewRows, getColumnProps, anyActive, reset }
}
