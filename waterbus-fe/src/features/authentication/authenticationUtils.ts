import type { AuthUser } from './authenticationTypes'

/** True when the user holds at least one of `roles`. An empty list means any signed-in user. */
export function hasAnyRole(user: AuthUser | null, roles: readonly string[] = []): boolean {
  if (!user) return false
  return roles.length === 0 || user.roles.some((role) => roles.includes(role))
}
