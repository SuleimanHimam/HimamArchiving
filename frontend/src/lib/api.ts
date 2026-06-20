import axios from 'axios'
import { auth, type AuthUser } from './auth'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5105/api'

export const api = axios.create({ baseURL })

// Attach the bearer token to every request.
api.interceptors.request.use((config) => {
  const token = auth.getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// On 401/403, clear the session so a stale or permission-less token doesn't block the user.
api.interceptors.response.use(
  (r) => r,
  (error) => {
    const status = error.response?.status
    if ((status === 401 || status === 403) && auth.isAuthenticated()) {
      auth.clear()
      window.location.replace('/login')
    }
    return Promise.reject(error)
  },
)

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: AuthUser
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/auth/login', { email, password })
  auth.setSession(data.accessToken, data.refreshToken, data.user)
  return data
}
