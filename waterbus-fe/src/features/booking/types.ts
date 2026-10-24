/**
 * The backend keeps no status on a seat: for a trip it is derived from the tickets
 * (Pending -> held, Valid / CheckedIn -> sold), plus the Redis hold other visitors have.
 */
export type SeatStatus = 'available' | 'held' | 'sold'

/** Cabin of the boat (BE `SeatCategory`: FrontCabin / Standard / Outdoor). */
export type SeatCategory = 'front' | 'standard' | 'outdoor'

export interface Seat {
  /** Seat id used when booking (a Guid from the backend). */
  id: string
  /** Seat code printed on the seat, e.g. "F01", "S12", "O03". */
  code: string
  category: SeatCategory
  /** Row inside its cabin, starting at 1. */
  row: number
  /** Column letter A-D (BE `ColumnNumber` 1-4). A and D are window seats. */
  column: 'A' | 'B' | 'C' | 'D'
  /** Fare multiplier of the seat (BE `PriceMultiplier`), 1 for a standard seat. */
  priceMultiplier: number
  status: SeatStatus
  window: boolean
  /** Fare of this seat for the chosen journey, from the backend (flat per trip type and seat class). */
  priceVnd?: number
}

/** The stops a booking boards and leaves at: real `TripStopCall`s of the trip (BR-ROUTE). */
export interface Journey {
  boardingCallId: string
  disembarkingCallId: string
  boardingVisitOrder: number
  disembarkingVisitOrder: number
}

export interface SeatMap {
  vesselName: string
  seats: Seat[]
  /** Set for a backend seat map; absent for the mock one. */
  journey?: Journey
  /** False when sales are closed for the trip (backend `salesState` is not `open`). */
  canBook?: boolean
}

/** Seat changes pushed in real time (seat-sync hub `seatHeld` / `seatReleased` / `seatSold`). */
export interface SeatEvent {
  type: 'SeatLocked' | 'SeatUnlocked' | 'SeatSold'
  tripId: string
  seatId: string
  /** The journey segment the change applies to; absent for mock events (whole trip). */
  boardingVisitOrder?: number
  disembarkingVisitOrder?: number
}

/** Who pays and receives the e-tickets (BR-COM-07): name and email required, phone optional. */
export interface PurchaserForm {
  fullName: string
  phoneCode: string
  phone: string
  email: string
}

/** Who sits on one seat (BR-COM-06): name required, email and phone optional. */
export interface SeatPassengerForm {
  fullName: string
  email: string
  phone: string
}

export interface BookingStep {
  label: string
  detail: string
}

// ---- Backend seat map (GET /trips/{tripId}/seats) ------------------------------------

export interface TripSeatDto {
  seatId: string
  seatCode: string
  rowNumber: number
  columnNumber: number
  seatClassId: string
  seatClassName: string | null
  price: number | null
  currency: string
  /** available | held | sold | unavailable */
  status: string
  unavailableReason: string | null
  holdExpiresAt: string | null
}

export interface TripSeatMapDto {
  tripId: string
  boatId: string
  boardingCallId: string
  disembarkingCallId: string
  boardingVisitOrder: number
  disembarkingVisitOrder: number
  canBook: boolean
  salesState: string
  availableSeatsCount: number
  asOf: string
  seats: TripSeatDto[]
}

export interface TripStopDto {
  callId: string
  stationId: string
  stationName: string
  visitOrder: number
  canBoard: boolean
  canAlight: boolean
  status: string
  estimatedArrivalTime?: string | null
  estimatedDepartureTime?: string | null
  /** Check-in window of this stop, set by operations (null while not configured). */
  checkInOpenAt?: string | null
  checkInCloseAt?: string | null
}

/** `GET /trips/{tripId}` (TripDetailDto), the parts used to pick the journey. */
export interface TripDetailDto {
  tripId: string
  routeId: string
  routeName: string
  tripType: string
  status: string
  departureTime: string
  arrivalTime: string
  canBook: boolean
  salesState: string
  boat: { boatId: string; code: string; name: string } | null
  stops: TripStopDto[]
}

// ---- Checkout (backend `/api/v1/checkouts`, CheckoutsController) -------------------

/** One passenger of a leg, in the order of `seatIds` (BE `PassengerInput`). */
export interface PassengerInput {
  name: string
  email?: string
  phone?: string
}

/** One trip of a checkout (BE `CheckoutLeg`). */
export interface CheckoutLeg {
  tripId: string
  seatIds: string[]
  boardingStationId: string
  disembarkingStationId: string
  boardingCallId?: string
  disembarkingCallId?: string
  passengers?: PassengerInput[]
}

/** Body of `POST /checkouts/holds` (BE `CreateCheckoutHoldCommand`): seats first, buyer later. */
export interface CreateHoldRequest {
  legs: CheckoutLeg[]
}

/** One passenger in the submit body, tied to the held seat (BE `CheckoutPassenger`). */
export interface SubmitPassenger {
  bookingId: string
  seatId: string
  name: string
  email?: string
  phone?: string
}

/** Body of `POST /checkouts/{orderId}/submit` (BE `SubmitCheckoutCommand`): buyer and passengers. */
export interface SubmitCheckoutRequest {
  customerName: string
  customerEmail: string
  /** 10-11 digits; left out when the purchaser gave none. */
  customerPhone?: string
  passengers: SubmitPassenger[]
}

/** A booking inside the create response (BE `BookingResponseDto`). */
export interface BookingResponse {
  bookingId: string
  bookingCode: string
  totalAmount: number
  status: string
  /** Seconds left on the hold when the response was made. */
  holdDurationSeconds: number
  orderId: string
  orderStatus: string
}

/** Response of `POST /checkouts/holds` (BE `CheckoutResponseDto`). */
export interface CheckoutResponse {
  orderId: string
  quotedTotal: number
  currency: string
  /** BE `OrderStatus`: PendingPayment right after creation. */
  status: string
  /** When the hold ends (ISO 8601). */
  expiresAt: string | null
  /** Guest token of the order; only returned here, so it is kept (header `X-Guest-Token`). */
  accessToken: string | null
  bookings: BookingResponse[]
}

/** BE `OrderStatus`. Only `Confirmed` means the payment was verified and the tickets issued. */
export type OrderStatus = 'Draft' | 'PendingPayment' | 'Confirmed' | 'Cancelled' | 'Expired'

export interface CheckoutTicket {
  ticketId: string
  ticketCode: string
  passengerName: string
  seatCode: string
  status: string
  boardingStatus: string
  paidFare: number
}

export interface CheckoutBooking {
  bookingId: string
  bookingCode: string
  publicBookingId: string
  tripId: string
  boardingCallId: string
  disembarkingCallId: string
  status: string
  quotedTotal: number
  tickets: CheckoutTicket[]
}

/** Response of `GET /checkouts/{orderId}` (BE `CheckoutDetailDto`): the truth after paying. */
export interface CheckoutDetail {
  orderId: string
  status: OrderStatus
  quotedTotal: number
  expiresAt: string | null
  bookings: CheckoutBooking[]
  payments: {
    transactionId: string
    type: string
    status: string
    amount: number
    requiresReconciliation: boolean
  }[]
}

/** The order whose seats are being held while the traveler pays. */
export interface BookingHold {
  orderId: string
  /** The booking of the single leg; the submit body names passengers per booking and seat. */
  bookingId: string
  /** Seats the hold covers, to tell when the choice changed and a new hold is needed. */
  seatIds: string[]
  /** True once buyer and passengers were submitted; the order is then awaiting payment. */
  submitted: boolean
  /** Guest token of the order; null for a mock order. */
  accessToken: string | null
  bookingCodes: string[]
  totalAmount: number
  /** When the hold ends, in ms since epoch (the backend `expiresAt`). */
  expiresAt: number
  /**
   * Every booking of the order, in leg order: one for one-way, two for a round trip (BR-COM-02).
   * `bookingId` and `seatIds` above are the first (outbound) one.
   */
  bookings?: { bookingId: string; tripId: string | null; seatIds: string[] }[]
}
