/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  /** Active fares per trip type and seat class (public, read only). */
  fares: '/api/v1/fares',
  boats: '/api/v1/boats',
  station: (id: string) => `/api/v1/stations/${id}`,
  boat: (id: string) => `/api/v1/boats/${id}`,
  /** Admin: seats of a boat and how many of them are active. */
  boatSeats: (boatId: string) => `/api/v1/boats/${boatId}/seats`,
  boatSeat: (boatId: string, seatId: string) => `/api/v1/boats/${boatId}/seats/${seatId}`,
  seatClasses: '/api/v1/seat-classes',
  seatClass: (id: string) => `/api/v1/seat-classes/${id}`,
  /** Admin catalogue. A route, schedule or fare is switched with `POST {item}/activate|deactivate`. */
  admin: {
    scannerDevices: '/api/v1/admin/scanner-devices',
    routes: '/api/v1/admin/routes',
    route: (id: string) => `/api/v1/admin/routes/${id}`,
    routeStops: (id: string) => `/api/v1/admin/routes/${id}/stops`,
    schedules: '/api/v1/admin/schedules',
    schedule: (id: string) => `/api/v1/admin/schedules/${id}`,
    scheduleStops: (id: string) => `/api/v1/admin/schedules/${id}/stops`,
    fares: '/api/v1/admin/fares',
    fare: (id: string) => `/api/v1/admin/fares/${id}`,
    /** Creates the trips of a schedule for a range of days. */
    generateTrips: '/api/v1/admin/trips/generate',
    /** Creates one trip outside any schedule (BR-SCH-10). */
    trips: '/api/v1/admin/trips',
    /** Assigns or replaces the boat of a trip that is still `Scheduled`. */
    tripBoat: (tripId: string) => `/api/v1/admin/trips/${tripId}/boat`,
  },
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
    scannerStaff: '/api/v1/checkins/assignments/staff',
    assignments: '/api/v1/checkins/assignments',
    stopAssignments: (callId: string) => '/api/v1/checkins/assignments/stops/' + callId,
    revokeAssignment: (id: string) => '/api/v1/checkins/assignments/' + id + '/revoke',
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
    /** Passenger: links an order bought as a guest to the signed-in account (BR-ID-04). */
    claim: (orderId: string) => `/api/v1/checkouts/${orderId}/claim`,
    /** Replaces the Manage Order link; the old token stops working at once (BR-ID-07). */
    rotateAccess: (orderId: string) => `/api/v1/checkouts/${orderId}/access/rotate`,
    /** Revokes every Manage Order and TripAccess link of the order. */
    revokeAccess: (orderId: string) => `/api/v1/checkouts/${orderId}/access/revoke`,
    /** Issues a Passenger TripAccess link for one paid booking (BR-ID-05). */
    tripAccess: (orderId: string, bookingId: string) =>
      `/api/v1/checkouts/${orderId}/bookings/${bookingId}/trip-access`,
  },
  me: {
    /** Passenger: orders of the signed-in account, newest first, paged. */
    orders: '/api/v1/me/orders',
  },
  /** Journey behind a TripAccess link; the token travels in `X-Trip-Access-Token`. */
  tripAccess: '/api/v1/tripaccess',
  payments: {
    createVnPayUrl: '/api/v1/payments/create-vnpay-url',
    vnPayReturn: '/api/v1/payments/vnpay-return',
    reconcile: (orderId: string, paymentId: string) =>
      `/api/v1/payments/${orderId}/attempts/${paymentId}/reconcile`,
  },
  refunds: {
    /** Admin: hoàn tiền một Booking của chuyến bị huỷ/chấm dứt. */
    request: '/api/v1/refunds/request',
    /** Admin: xác nhận đã hoàn tay khi cổng thanh toán không hoàn được. */
    confirmManual: '/api/v1/refunds/confirm-manual',
    reconcile: (refundId: string) => `/api/v1/refunds/${refundId}/reconcile`,
    /** Admin: Booking của một chuyến bị huỷ/chấm dứt cần hoàn (chỉ đọc, phân trang). */
    candidates: '/api/v1/refunds/candidates',
    /** Admin: tạo yêu cầu hoàn cho mọi Booking đủ điều kiện của một chuyến. */
    batchRequest: '/api/v1/refunds/batch-request',
    /** Admin: các khoản hoàn đang chờ nhân viên hoàn (chỉ đọc, phân trang). */
    pending: '/api/v1/refunds/pending',
    /** Admin: tải CSV các khoản đang chờ hoàn. */
    exportCsv: '/api/v1/refunds/export',
    /** Admin: xác nhận đã hoàn nhiều khoản một lần. */
    confirmManualBatch: '/api/v1/refunds/confirm-manual/batch',
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
