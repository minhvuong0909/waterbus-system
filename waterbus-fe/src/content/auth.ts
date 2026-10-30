import type { AuthShowcase } from '../features/authentication/authenticationTypes'

// Nội dung biên tập của màn đăng nhập, không có API.
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
