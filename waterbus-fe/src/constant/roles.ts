/** Role names exactly as the backend seeds and issues them (ApplicationDbContextInitializer). */
export const ROLES = {
  admin: 'Admin',
  captain: 'Captain',
  staff: 'Staff',
  passenger: 'Passenger',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]

/** Roles allowed in the captain's portal. */
export const CAPTAIN_ROLES = [ROLES.captain, ROLES.admin] as const

/** Roles that work in the operations area at all. */
export const STAFF_ROLES = [ROLES.admin, ROLES.captain] as const
