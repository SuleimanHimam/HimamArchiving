import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { auth } from '../lib/auth'

/**
 * The inverse of ProtectedRoute: keeps a signed-in user off the login screen.
 *
 * Without it, /login stays reachable while authenticated — a back-button press after signing in
 * lands on a login form the user has already satisfied, and signing in again mints a second
 * session for no reason. If a redirect brought them here with a destination in tow, that is where
 * they go; otherwise home.
 */
export default function GuestRoute({ children }: { children: ReactNode }) {
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from

  if (auth.isAuthenticated()) return <Navigate to={from ?? '/app'} replace />

  return <>{children}</>
}
