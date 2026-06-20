import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,   // data stays fresh for 5 min
      gcTime: 10 * 60 * 1000,     // keep unused cache for 10 min
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})
