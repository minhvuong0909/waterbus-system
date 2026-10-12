import type { PassengerForm, Seat } from '../booking/types'
import type { SearchQuery } from '../trips/types'

export interface Ticket {
  id: string
  /** Vessel/trip code, e.g. "WB-01". */
  tripCode: string
  lineName: string
  /** Short label next to the trip code, e.g. "Return Route". */
  routeTag: string
  bookingRef: string
  /** Code encoded in the boarding QR. */
  ticketCode: string
  /** Display text, already in the active language. */
  status: string
  departTime: string
  departPier: string
  /** Short name without "Pier", e.g. "Bach Dang". */
  departShort: string
  departDistrict: string
  arriveTime: string
  arrivePier: string
  arriveShort: string
  arriveDistrict: string
  durationMins: number
  dateLabel: string
  /** Date without the year, e.g. "Dec 15". */
  dateShort: string
  passenger: string
  seat: string
  seatNote: string
  /** True for a window seat. */
  windowSeat: boolean
  /** True once the journey has been made. */
  completed: boolean
  fareVnd: number
  vesselNote: string
  gate: string
}

/**
 * What is stored for a booking made in this session. The display ticket is rebuilt from it in the
 * active language every time, so switching language never leaves half-translated tickets behind.
 */
export interface BookedSnapshot {
  id: string
  tripId: string
  query: SearchQuery
  seat: Seat | undefined
  seatId: string
  passenger: PassengerForm
  totalVnd: number
}

export interface TicketWallet {
  next: Ticket
  later: Ticket[]
  past: Ticket[]
}

export interface VoucherCredit {
  code: string
  description: string
  valueVnd: number
}

export interface ManageBooking {
  bookingCode: string
  ticket: Ticket
  voucherCredit: VoucherCredit[]
}

export interface ChatMessage {
  id: number
  from: 'user' | 'assistant'
  text: string
}
