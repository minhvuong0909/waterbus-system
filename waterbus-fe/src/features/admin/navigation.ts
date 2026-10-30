import { ROLES } from '../../constant/roles'
import { ROUTES } from '../../routes/routes'

export interface AdminNavItem {
  to: string
  label: string
  icon: string
  /** Roles that may open the page. */
  roles: readonly string[]
}

const { admin, captain } = ROLES

/** Điều hướng khu vực quản trị; công cụ kiểm thử chỉ xuất hiện ở môi trường phát triển. */
export const ADMIN_NAV: AdminNavItem[] = [
  { to: ROUTES.admin, label: 'Overview', icon: 'dashboard', roles: [admin] },
  { to: ROUTES.adminRadar, label: 'Live Operations', icon: 'radar', roles: [admin] },
  { to: ROUTES.adminTrips, label: 'Trips', icon: 'directions_boat', roles: [admin] },
  { to: ROUTES.adminStations, label: 'Stations', icon: 'anchor', roles: [admin] },
  { to: ROUTES.adminBoats, label: 'Boats', icon: 'sailing', roles: [admin] },
  { to: ROUTES.adminFares, label: 'Fares', icon: 'payments', roles: [admin] },
  { to: ROUTES.adminIncidents, label: 'Incidents', icon: 'warning', roles: [admin] },
  { to: ROUTES.adminAudioGuides, label: 'Audio guides', icon: 'campaign', roles: [admin] },
  ...(import.meta.env.DEV
    ? [{ to: ROUTES.adminApi, label: 'Test API', icon: 'api', roles: [admin] }]
    : []),
]

/** Roles allowed in the captain's portal. */
export const CAPTAIN_ROLES = [captain, admin] as const

/** Roles that work in the operations area at all. */
export const STAFF_ROLES = [admin, captain] as const

/** Where a user lands after signing in. */
export function landingRouteFor(roles: readonly string[]): string {
  if (roles.includes(admin)) return ROUTES.admin
  if (roles.includes(captain)) return ROUTES.captain
  return ROUTES.myTickets
}
