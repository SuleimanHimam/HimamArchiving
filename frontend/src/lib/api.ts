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

// On 401, clear the session (a refresh-token flow can be added here later).
api.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401 && auth.isAuthenticated()) {
      auth.clear()
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
