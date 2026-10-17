/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  boats: '/api/v1/boats',
  checkouts: {
    /** Holds seats first (Draft); the buyer is submitted later. */
    holds: '/api/v1/checkouts/holds',
    byId: (orderId: string) => `/api/v1/checkouts/${orderId}`,
    submit: (orderId: string) => `/api/v1/checkouts/${orderId}/submit`,
    qr: (orderId: string, bookingId: string) =>
      `/api/v1/checkouts/${orderId}/bookings/${bookingId}/qr`,
  },
  payments: {
    createVnPayUrl: '/api/v1/payments/create-vnpay-url',
    vnPayReturn: '/api/v1/payments/vnpay-return',
    reconcile: (orderId: string, paymentId: string) =>
      `/api/v1/payments/${orderId}/attempts/${paymentId}/reconcile`,
  },
  trips: {
    search: '/api/v1/trips/search',
    /** Public detail of a trip with its real stops (`TripStopCall`s). */
    detail: (tripId: string) => `/api/v1/trips/${tripId}`,
    /** Seat map and fares for one journey segment; creates no hold. */
    seats: (tripId: string) => `/api/v1/trips/${tripId}/seats`,
    /** Trips for operations: all boats for Admin, the captain's own boat for a Captain. */
    operations: '/api/v1/trips/operations',
    /** Audit log of status changes (Admin). */
    operationEvents: '/api/v1/trips/operations/events',
    status: (tripId: string) => `/api/v1/trips/${tripId}/status`,
    sos: (tripId: string) => `/api/v1/trips/${tripId}/sos`,
  },
  tracking: {
    /** Latest position of every active boat (Admin). */
    boats: '/api/v1/tracking/boats',
  },
  incidents: {
    list: '/api/v1/incidents',
    detail: (id: string) => `/api/v1/incidents/${id}`,
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
  tripStatusChanged: 'tripStatusChanged',
  incidentRaised: 'incidentRaised',
  seatHeld: 'seatHeld',
  seatReleased: 'seatReleased',
  seatSold: 'seatSold',
} as const
