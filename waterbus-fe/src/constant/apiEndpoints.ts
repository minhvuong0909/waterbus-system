/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  openApi: '/swagger/v1/swagger.json',
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  /** Active fares per trip type and seat class (public, read only). */
  fares: '/api/v1/fares',
  boats: '/api/v1/boats',
  station: (id: string) => `/api/v1/stations/${id}`,
  boat: (id: string) => `/api/v1/boats/${id}`,
  assistant: '/api/v1/aiassistant/chat',
  textToSpeech: {
    /** Admin: Vietnamese script → MP3 with Azure TTS, stored in private Blob storage. */
    generate: '/api/v1/texttospeech/generate',
    /** Admin: the stored MP3 of an `assetKey`, to listen before publishing. */
    audio: '/api/v1/texttospeech/audio',
  },
  audioGuides: {
    /** Admin: the POIs of the database with the state of their Vietnamese audio guide. */
    pois: '/api/v1/audioguides/pois',
    /** Admin: links a generated MP3 to a POI and publishes it. */
    publish: '/api/v1/audioguides/publish',
  },
  checkIns: {
    /** Admin: the check-in window of one stop (`TripStopCall`). */
    window: (tripStopCallId: string) => `/api/v1/checkins/windows/${tripStopCallId}`,
  },
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
    /** Current weather of every active route, read only (Admin). */
    weatherNow: '/api/v1/incidents/weather-now',
    /** Closes an open incident without changing any trip (Admin). */
    resolve: (id: string) => `/api/v1/incidents/${id}/resolve`,
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
