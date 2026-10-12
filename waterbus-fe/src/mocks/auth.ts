import type { AuthShowcase, AuthUser } from '../features/authentication/authenticationTypes'

export const MOCK_USER: AuthUser = {
  id: 'user-001',
  fullName: 'Nguyen Van An',
  email: 'passenger@smartwaterbus.vn',
}

// Pre-filled values shown in the sign-in form, as in the design.
export const SIGN_IN_DEFAULTS = {
  email: 'passenger@smartwaterbus.vn',
  password: 'waterbus2025',
}

export const AUTH_SHOWCASE: AuthShowcase = {
  eyebrow: 'Urban River Flow Network',
  headline: ['Your River Journey', 'Starts Here'],
  intro:
    'Manage your bookings, view digital tickets, and plan your crossings across the river corridor.',
  crossing: {
    corridor: 'Central River Corridor',
    duration: '12 Min Crossing',
    from: 'Bach Dang',
    to: 'Thu Thiem',
    vessel: 'WB-01',
  },
  highlights: [
    'Digital tickets and boarding passes in one place',
    'Access your saved river trips and pier schedules',
  ],
}
