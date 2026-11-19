import { useQuery } from '@tanstack/react-query'
import { hrApi } from './hr'

/** Active employees in the caller's scope (first 500), cached. */
export function useEmployeeOptions(enabled = true) {
  return useQuery({
    queryKey: ['hr', 'employeeOptions'],
    queryFn: () => hrApi.employees({ status: 'Active', page: 1, pageSize: 500 }),
    staleTime: 60_000,
    enabled,
  }).data?.items ?? []
}

export const thisMonth = () => {
  const now = new Date()
  return { year: now.getFullYear(), month: now.getMonth() + 1 }
}

/** Today's date on the Asia/Hebron calendar, YYYY-MM-DD. */
export const todayHebron = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Hebron', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
