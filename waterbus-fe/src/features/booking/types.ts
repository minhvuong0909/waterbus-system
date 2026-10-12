export type SeatStatus = 'available' | 'occupied'

/** Standard seats are on the main deck; VIP seats are in the aft lounge and cost extra. */
export type SeatTier = 'standard' | 'vip'

export interface Seat {
  id: string
  row: number
  /** Column letter A-D. A and D are window seats. */
  column: 'A' | 'B' | 'C' | 'D'
  status: SeatStatus
  tier: SeatTier
  window: boolean
}

export interface SeatMap {
  deckName: string
  vesselModel: string
  rows: number[]
  seats: Seat[]
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
