import type { AuthShowcase, AuthUser } from '../features/authentication/authenticationTypes'

// The backend has no register or Google sign-in endpoint yet, so these flows end with this
// passenger. Real sign-in does not use it.
export const MOCK_USER: AuthUser = {
  id: 'user-001',
  fullName: 'Nguyen Van An',
  email: 'passenger@smartwaterbus.vn',
  roles: ['Passenger'],
}

export const AUTH_SHOWCASE: AuthShowcase = {
  eyebrow: 'Urban River Flow Network',
  headline: ['Your River Journey', 'Starts Here'],
  intro:
    'Manage your bookings, view digital tickets, and plan your crossings across the river corridor.',
  crossing: {
    corridor: 'Central River Corridor',
    duration: '45 Min Crossing',
    from: 'Bạch Đằng',
    to: 'Linh Đông',
    vessel: 'SWB-01',
  },
  highlights: [
    'Digital tickets and boarding passes in one place',
    'Access your saved river trips and pier schedules',
  ],
}
