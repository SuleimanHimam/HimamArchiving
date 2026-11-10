import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { auth } from '../lib/auth'

interface Props {
  children: ReactNode
  /**
   * Restrict to System Admin (tenants, system roles, chain verification, keys,
   * backup/restore, monitoring). Guarded by a direct role check rather than the
   * generic permission hook, so a permission grant can never open a system route.
   */
  systemOnly?: boolean
  /** Module permission required. Ignored for System Admin, who passes all routes. */
  perm?: string
}

export default function ProtectedRoute({ children, systemOnly, perm }: Props) {
  const location = useLocation()

  // The requested URL travels to the login screen so signing in returns the user to the page they
  // asked for — a bookmark to one document should not land everyone on the dashboard.
  if (!auth.isAuthenticated()) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  if (systemOnly) {
    // Direct role check — not auth.hasPermission(), which System Admin short-circuits.
    return auth.isSystemAdmin() ? <>{children}</> : <Navigate to="/app" replace />
  }

  // System Admin passes every module route without a permission check.
  if (perm && !auth.isSystemAdmin() && !auth.hasPermission(perm)) {
    return <Navigate to="/app" replace />
  }

  return <>{children}</>
}
