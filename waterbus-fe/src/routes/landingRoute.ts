import { ROLES } from '../constant/roles'
import { ROUTES } from './routes'

/** Where a user lands after signing in. */
export function landingRouteFor(roles: readonly string[]): string {
  if (roles.includes(ROLES.admin)) return ROUTES.admin
  if (roles.includes(ROLES.captain)) return ROUTES.captain
  return ROUTES.myTickets
}
