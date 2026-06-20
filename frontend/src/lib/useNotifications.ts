import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { notifications } from './notifications'
import { auth } from './auth'

export const NOTIFICATIONS_KEY = ['notifications'] as const
export const UNREAD_COUNT_KEY = ['notifications', 'unreadCount'] as const

export function useNotifications(unreadOnly = false) {
  return useQuery({
    queryKey: [...NOTIFICATIONS_KEY, unreadOnly],
    queryFn: () => notifications.list(unreadOnly),
    enabled: auth.isAuthenticated(),
    refetchInterval: 30_000,
  })
}

export function useUnreadCount() {
  return useQuery({
    queryKey: UNREAD_COUNT_KEY,
    queryFn: notifications.unreadCount,
    enabled: auth.isAuthenticated(),
    refetchInterval: 30_000,
    initialData: 0,
  })
}

export function useMarkRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => notifications.markRead(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: NOTIFICATIONS_KEY })
      qc.invalidateQueries({ queryKey: UNREAD_COUNT_KEY })
    },
  })
}

export function useMarkAllRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => notifications.markAllRead(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: NOTIFICATIONS_KEY })
      qc.invalidateQueries({ queryKey: UNREAD_COUNT_KEY })
    },
  })
}
