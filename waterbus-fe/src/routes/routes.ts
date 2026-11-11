/** Every route path in the app. Use these constants instead of string literals. */
export const ROUTES = {
  home: '/',
  signIn: '/sign-in',
  searchResults: '/search/results',
  tripDetail: '/trip',
  seatSelection: '/booking/seats',
  passengerDetails: '/booking/passenger',
  checkoutReview: '/booking/review',
  payment: '/payment',
  paymentCallback: '/payment/callback',
  bookingSuccess: '/payment/success',
  myTickets: '/tickets',
  ticketDetail: '/tickets/detail',
  manageBooking: '/tickets/manage',
  /** Secure link to one booking, sent to the traveler. `:token` is issued by the backend. */
  manageByLink: '/manage/:token',
  /** Passenger: every order of the signed-in account. */
  orderHistory: '/account/orders',
  /** Passenger TripAccess Link shared by the buyer; shows the journey only (BR-ID-05). */
  sharedTrip: '/shared-trip/:token',
  liveTracking: '/tracking',
  explore: '/explore',
  help: '/help',
  // Operations area (Admin) and the captain's portal.
  admin: '/admin',
  adminRadar: '/admin/radar',
  adminTrips: '/admin/trips',
  adminStations: '/admin/stations',
  adminBoats: '/admin/boats',
  adminFares: '/admin/fares',
  adminIncidents: '/admin/incidents',
  adminRefunds: '/admin/refunds',
  adminCheckIn: '/admin/check-in',
  adminAudioGuides: '/admin/audio-guides',
  captain: '/captain',
} as const
