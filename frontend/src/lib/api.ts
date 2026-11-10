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

/* Only 401 ends the session. 403 means "you are signed in and this particular call is not
   yours" — it says nothing about the token. Treating it as a logout meant any optional,
   permission-gated fetch on a shared screen threw the user back to the login page: an
   Employee opening the app fetched the classification catalogue, got a 403, and was signed
   out before the dashboard finished rendering. Callers handle their own 403s. */
api.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401 && auth.isAuthenticated()) {
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
  auth.setSession(data.accessToken, data.refreshToken, data.user, data.expiresAt)
  return data
}
