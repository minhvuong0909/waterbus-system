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

export type PassengerCategory = 'adult' | 'senior' | 'child'

export interface PassengerForm {
  category: PassengerCategory
  fullName: string
  /** DD / MM / YYYY */
  dateOfBirth: string
  phoneCode: string
  phone: string
  email: string
  useAsContact: boolean
  assistance: string[]
}

export interface AssistanceOption {
  id: string
  icon: string
  title: string
  description: string
}

export interface Voucher {
  code: string
  /** Discount as a percentage of the fare, applied to whichever trip is currently selected. */
  percentOff: number
}

export interface BookingStep {
  label: string
  detail: string
}
