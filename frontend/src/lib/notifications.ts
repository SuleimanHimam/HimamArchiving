import { api } from './api'

export interface NotificationDto {
  id: number
  title: string
  body: string | null
  type: string
  entityType: string | null
  entityId: number | null
  isRead: boolean
  isEscalation: boolean
  createdAt: string
}

export interface NotificationListResult {
  items: NotificationDto[]
  unreadCount: number
}

export const notifications = {
  list: (unreadOnly = false) =>
    api.get<NotificationListResult>('/notifications', { params: { unreadOnly } }).then((r) => r.data),

  unreadCount: () => api.get<{ count: number }>('/notifications/unread-count').then((r) => r.data.count),

  markRead: (id: number) => api.post(`/notifications/${id}/read`),

  markAllRead: () => api.post('/notifications/read-all'),
}
