import type { ReactNode } from 'react'

interface PrivateRouteProps {
  children: ReactNode
}

// Intentionally a pass-through: there is no real auth yet and every page must stay reachable.
// Replace with a real guard once the backend exists.
export default function PrivateRoute({ children }: PrivateRouteProps) {
  return <>{children}</>
}
