/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  boats: '/api/v1/boats',
  bookings: '/api/v1/bookings',
  payments: {
    vnPayReturn: '/api/v1/payments/vnpay-return',
  },
  trips: {
    search: '/api/v1/trips/search',
  },
  tracking: {
    /** Latest position of every active boat (Admin). */
    boats: '/api/v1/tracking/boats',
  },
  incidents: {
    list: '/api/v1/incidents',
    /** Runs the weather guard now instead of waiting for its schedule (Admin). */
    weatherCheck: '/api/v1/incidents/weather-check',
  },
} as const

/** SignalR hubs (see docs/REALTIME_CONTRACT.md). */
export const HUBS = {
  boatTracking: '/hubs/boat-tracking',
  seatSync: '/hubs/seat-sync',
} as const

/** Event names the hubs push to the browser. */
export const HUB_EVENTS = {
  boatPosition: 'boatPosition',
  weatherAlert: 'weatherAlert',
  seatHeld: 'seatHeld',
  seatReleased: 'seatReleased',
  seatSold: 'seatSold',
} as const
