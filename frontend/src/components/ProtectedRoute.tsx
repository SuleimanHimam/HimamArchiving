import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { auth } from '../lib/auth'

export default function ProtectedRoute({ children }: { children: ReactNode }) {
  return auth.isAuthenticated() ? <>{children}</> : <Navigate to="/login" replace />
}
