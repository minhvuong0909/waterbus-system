/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  bookings: '/api/v1/bookings',
  payments: {
    vnPayReturn: '/api/v1/payments/vnpay-return',
  },
  trips: {
    search: '/api/v1/trips/search',
  },
} as const
