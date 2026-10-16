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
}

export interface SeatMap {
  vesselName: string
  seats: Seat[]
}

/** Seat hold changes pushed in real time (SignalR `SeatLocked` / `SeatUnlocked`). */
export interface SeatEvent {
  type: 'SeatLocked' | 'SeatUnlocked'
  tripId: string
  seatId: string
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
  passengers: PassengerInput[]
}

/** Body of `POST /checkouts` (BE `CreateCheckoutCommand`). */
export interface CreateCheckoutRequest {
  customerName: string
  customerEmail: string
  /** 10-11 digits; left out when the purchaser gave none. */
  customerPhone?: string
  legs: CheckoutLeg[]
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

/** Response of `POST /checkouts` (BE `CheckoutResponseDto`). */
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
  /** Guest token of the order; null for a mock order. */
  accessToken: string | null
  bookingCodes: string[]
  totalAmount: number
  /** When the hold ends, in ms since epoch (the backend `expiresAt`). */
  expiresAt: number
}
