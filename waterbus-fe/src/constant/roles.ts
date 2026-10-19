/** Role names exactly as the backend seeds and issues them (ApplicationDbContextInitializer). */
export const ROLES = {
  admin: 'Admin',
  captain: 'Captain',
  staff: 'Staff',
  passenger: 'Passenger',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]
