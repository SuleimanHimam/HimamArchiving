import { useQuery } from '@tanstack/react-query'
import { hrApi, type LookupCategory, type LookupDto } from './hr'
import { organization } from './organization'

/** Active values of one reference list, cached across the HR screens. */
export function useLookups(category: LookupCategory) {
  return useQuery({
    queryKey: ['hr', 'lookups', category],
    queryFn: () => hrApi.lookups(category, true),
    staleTime: 5 * 60_000,
  }).data ?? []
}

export function useOrgUnits() {
  return useQuery({ queryKey: ['hr', 'orgUnits'], queryFn: () => organization.orgUnits(), staleTime: 5 * 60_000 }).data ?? []
}

export function useSites() {
  return useQuery({ queryKey: ['hr', 'sites'], queryFn: () => hrApi.sites(), staleTime: 5 * 60_000 }).data ?? []
}

export const lookupName = (items: LookupDto[], id: number | null | undefined) =>
  id == null ? null : items.find((l) => l.id === id)?.name ?? null

/** yyyy-mm-dd, or the placeholder dash. Dates are calendar days (DateOnly) on the server. */
// Timestamps are stored in UTC and shown on the Asia/Hebron calendar (standards §4.4); en-CA gives YYYY-MM-DD.
const hebronDay = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Hebron', year: 'numeric', month: '2-digit', day: '2-digit' })

/** YYYY-MM-DD. Date-only values (2026-11-15) pass through; UTC timestamps are converted to Asia/Hebron. */
export const day = (v: string | null | undefined) => {
  if (!v) return '—'
  if (v.length === 10) return v
  const d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(v) ? v : `${v}Z`)
  return Number.isNaN(d.getTime()) ? v.slice(0, 10) : hebronDay.format(d)
}
