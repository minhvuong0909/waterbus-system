/** Role names exactly as the backend seeds and issues them (ApplicationDbContextInitializer). */
export const ROLES = {
  admin: 'Admin',
  dispatcher: 'Dispatcher',
  accountant: 'Accountant',
  captain: 'Captain',
  staff: 'Staff',
  passenger: 'Passenger',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]
