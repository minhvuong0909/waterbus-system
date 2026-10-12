import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { hasAnyRole } from '../features/authentication/authenticationUtils'
import { useAuth } from '../features/authentication/hook/authenticationHook'
import { ROUTES } from './routes'

interface PrivateRouteProps {
  children: ReactNode
  /** Roles allowed in (any one of them). Omit to allow every signed-in user. */
  roles?: readonly string[]
}

/**
 * Sends visitors who are not signed in to the sign-in page (remembering where they were going),
 * and signed-in users without a required role back to the home page.
 */
export default function PrivateRoute({ children, roles }: PrivateRouteProps) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to={ROUTES.signIn} state={{ from: location.pathname }} replace />
  if (!hasAnyRole(user, roles)) return <Navigate to={ROUTES.home} replace />
  return <>{children}</>
}
