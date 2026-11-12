import { ROLES } from '../../constant/roles'
import type { AuthUser } from '../../services/authStorage'

/** True when the user holds at least one of `roles`. An empty list means any signed-in user. */
export function hasAnyRole(user: AuthUser | null, roles: readonly string[] = []): boolean {
  if (!user) return false
  return roles.length === 0 || user.roles.some((role) => roles.includes(role))
}

/** True for a signed-in Passenger, the only role that owns orders (BR-ID-04). */
export const isPassenger = (user: AuthUser | null): boolean => hasAnyRole(user, [ROLES.passenger])
