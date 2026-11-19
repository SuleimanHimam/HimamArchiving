import { useQuery } from '@tanstack/react-query'
import { supply } from './supply'

/* Cached supply look-ups shared by the screens. */

export function useWarehouses() {
  return useQuery({ queryKey: ['supply', 'warehouses'], queryFn: supply.warehouses, staleTime: 60_000 }).data ?? []
}

export function useItems(activeOnly = true) {
  return useQuery({ queryKey: ['supply', 'items', activeOnly], queryFn: () => supply.items(activeOnly), staleTime: 60_000 }).data ?? []
}
